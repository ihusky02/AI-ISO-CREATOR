using System.Collections.Concurrent;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using AI_ISO_Creator.Core.Models;
using Microsoft.Extensions.Logging;

namespace AI_ISO_Creator.Core.Services;

/// <summary>
/// Orchestrates Linux ISO generation inside isolated, rootless Podman containers.
/// Manages build steps, real-time log capturing, SquashFS compression, bootloader configuration, and QEMU execution.
/// </summary>
public class ContainerBuilderService : IIsoBuilderService
{
    private readonly ILogger<ContainerBuilderService> _logger;
    private readonly ConcurrentDictionary<string, BuildJob> _jobs = new();
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _cancellationTokens = new();
    private readonly string _buildsRootDirectory;

    public event Action<string, BuildLogEntry>? OnLogReceived;
    public event Action<string, BuildJob>? OnJobUpdated;

    public ContainerBuilderService(ILogger<ContainerBuilderService> logger)
    {
        _logger = logger;
        _buildsRootDirectory = Path.Combine(Directory.GetCurrentDirectory(), "build_artifacts");
        Directory.CreateDirectory(_buildsRootDirectory);
    }

    /// <summary>
    /// Checks whether the Podman container engine binary is accessible and executable.
    /// </summary>
    public bool IsPodmanAvailable()
    {
        try
        {
            using var proc = Process.Start(new ProcessStartInfo
            {
                FileName = "podman",
                Arguments = "--version",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });
            proc?.WaitForExit(3000);
            return proc?.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Checks whether the QEMU x86_64 emulator binary is installed and executable.
    /// </summary>
    public bool IsQemuAvailable()
    {
        try
        {
            using var proc = Process.Start(new ProcessStartInfo
            {
                FileName = "qemu-system-x86_64",
                Arguments = "--version",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });
            proc?.WaitForExit(3000);
            return proc?.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    public BuildJob? GetJob(string jobId)
    {
        _jobs.TryGetValue(jobId, out var job);
        return job;
    }

    public IEnumerable<BuildJob> GetAllJobs()
    {
        return _jobs.Values.OrderByDescending(j => j.CreatedAt);
    }

    public bool CancelJob(string jobId)
    {
        if (_cancellationTokens.TryGetValue(jobId, out var cts))
        {
            cts.Cancel();
            if (_jobs.TryGetValue(jobId, out var job))
            {
                job.Status = BuildStatus.Cancelled;
                job.StatusMessage = "Build operation was cancelled by user.";
                job.FinishedAt = DateTime.UtcNow;
                AddLog(job, "warn", "Build operation was cancelled by user.");
                NotifyJobUpdated(job);
            }
            return true;
        }
        return false;
    }

    public bool DeleteJob(string jobId)
    {
        CancelJob(jobId);
        var removed = _jobs.TryRemove(jobId, out _);
        var dir = Path.Combine(_buildsRootDirectory, jobId);
        if (Directory.Exists(dir))
        {
            try
            {
                Directory.Delete(dir, true);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete workspace directory for job {JobId}", jobId);
            }
        }
        return removed;
    }

    public Task<bool> LaunchInQemuAsync(string jobId)
    {
        if (!IsQemuAvailable())
        {
            return Task.FromResult(false);
        }

        var job = GetJob(jobId);
        if (job == null || string.IsNullOrWhiteSpace(job.OutputIsoPath) || !File.Exists(job.OutputIsoPath))
        {
            return Task.FromResult(false);
        }

        try
        {
            var kvmFlag = File.Exists("/dev/kvm") ? "-enable-kvm " : "";
            var psi = new ProcessStartInfo
            {
                FileName = "qemu-system-x86_64",
                Arguments = $"{kvmFlag}-m 2048 -cdrom \"{job.OutputIsoPath}\" -boot d -vga std",
                UseShellExecute = true
            };
            Process.Start(psi);
            AddLog(job, "info", "Launched QEMU virtual machine with the generated ISO image.");
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error launching QEMU for build job {JobId}", jobId);
            AddLog(job, "error", $"Failed to launch QEMU: {ex.Message}");
            return Task.FromResult(false);
        }
    }

    public Task<BuildJob> StartBuildAsync(IsoRecipe recipe, CancellationToken cancellationToken = default)
    {
        var job = new BuildJob
        {
            Id = Guid.NewGuid().ToString("N")[..8],
            Recipe = recipe,
            Status = BuildStatus.Queued,
            StatusMessage = "Build task queued...",
            ProgressPercent = 5,
            CreatedAt = DateTime.UtcNow
        };

        _jobs[job.Id] = job;
        var jobCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _cancellationTokens[job.Id] = jobCts;

        AddLog(job, "info", $"Started new ISO build job [{job.Id}] for label: {recipe.IsoLabel} ({recipe.Distribution})");
        NotifyJobUpdated(job);

        // Run background build task
        _ = Task.Run(() => ExecuteBuildProcessAsync(job, jobCts.Token), jobCts.Token);

        return Task.FromResult(job);
    }

    private async Task ExecuteBuildProcessAsync(BuildJob job, CancellationToken ct)
    {
        job.StartedAt = DateTime.UtcNow;
        var jobWorkDir = Path.Combine(_buildsRootDirectory, job.Id);
        Directory.CreateDirectory(jobWorkDir);

        try
        {
            if (!IsPodmanAvailable())
            {
                throw new InvalidOperationException("Podman container engine is not available on this host.");
            }

            job.Status = BuildStatus.Initializing;
            job.StatusMessage = "Initializing workspace and generating build script...";
            job.ProgressPercent = 10;
            AddLog(job, "info", $"Job workspace directory: {jobWorkDir}");
            NotifyJobUpdated(job);

            // Generate build script
            var scriptPath = Path.Combine(jobWorkDir, "build.sh");
            var buildScriptContent = GenerateBuildScript(job.Recipe, job.Id);
            await File.WriteAllTextAsync(scriptPath, buildScriptContent, Encoding.UTF8, ct);

            if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
            {
                File.SetUnixFileMode(scriptPath, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                                               UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                                               UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
            }

            job.Status = BuildStatus.InstallingPackages;
            job.StatusMessage = "Starting Podman container and bootstrapping packages...";
            job.ProgressPercent = 25;
            AddLog(job, "info", "Starting build environment inside Podman container...");
            NotifyJobUpdated(job);

            var isoOutputFileName = $"{job.Recipe.IsoLabel.ToLowerInvariant()}-{job.Id}.iso";
            var distroLower = job.Recipe.Distribution.ToLowerInvariant();
            string containerImage;
            if (distroLower.Contains("fedora"))
            {
                containerImage = "fedora:40";
            }
            else if (distroLower.Contains("arch"))
            {
                containerImage = "archlinux:latest";
            }
            else if (distroLower.Contains("ubuntu"))
            {
                containerImage = "ubuntu:24.04";
            }
            else if (distroLower.Contains("debian"))
            {
                containerImage = "debian:bookworm-slim";
            }
            else if (distroLower.Contains("suse"))
            {
                containerImage = "opensuse/tumbleweed:latest";
            }
            else
            {
                containerImage = "alpine:3.20";
            }

            var (success, errorMsg) = await RunPodmanProcessAsync(job, jobWorkDir, containerImage, ct);

            if (!success)
            {
                throw new InvalidOperationException(errorMsg ?? "Unknown error occurred inside the build container.");
            }

            var resultingIsoPath = Path.Combine(jobWorkDir, "output", isoOutputFileName);
            if (!File.Exists(resultingIsoPath))
            {
                var outputDir = Path.Combine(jobWorkDir, "output");
                var isoFiles = Directory.Exists(outputDir) ? Directory.GetFiles(outputDir, "*.iso") : Array.Empty<string>();
                if (isoFiles.Length > 0)
                {
                    resultingIsoPath = isoFiles[0];
                }
                else
                {
                    throw new FileNotFoundException("Generated ISO image was not found in the output directory.");
                }
            }

            var fileInfo = new FileInfo(resultingIsoPath);
            job.OutputIsoPath = resultingIsoPath;
            job.OutputIsoFileName = Path.GetFileName(resultingIsoPath);
            job.OutputIsoSize = fileInfo.Length;

            job.Status = BuildStatus.CreatingIso;
            job.StatusMessage = "Computing SHA256 checksum...";
            job.ProgressPercent = 95;
            AddLog(job, "info", $"Generated ISO file: {job.OutputIsoFileName} ({FormatBytes(job.OutputIsoSize)})");
            NotifyJobUpdated(job);

            job.OutputIsoSha256 = await ComputeSha256Async(resultingIsoPath, ct);
            AddLog(job, "success", $"SHA256 checksum: {job.OutputIsoSha256}");

            job.Status = BuildStatus.Completed;
            job.StatusMessage = "ISO image successfully created and ready for download!";
            job.ProgressPercent = 100;
            job.FinishedAt = DateTime.UtcNow;
            AddLog(job, "success", "Build process completed successfully.");
            NotifyJobUpdated(job);
        }
        catch (OperationCanceledException)
        {
            job.Status = BuildStatus.Cancelled;
            job.StatusMessage = "Build operation was cancelled.";
            job.FinishedAt = DateTime.UtcNow;
            AddLog(job, "warn", "Operation cancelled.");
            NotifyJobUpdated(job);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Build failure for job {JobId}", job.Id);
            job.Status = BuildStatus.Failed;
            job.StatusMessage = $"Build failed: {ex.Message}";
            job.ErrorMessage = ex.ToString();
            job.FinishedAt = DateTime.UtcNow;
            AddLog(job, "error", $"CRITICAL BUILD ERROR: {ex.Message}");
            NotifyJobUpdated(job);
        }
        finally
        {
            _cancellationTokens.TryRemove(job.Id, out _);
        }
    }

    private async Task<(bool success, string? errorMessage)> RunPodmanProcessAsync(
        BuildJob job,
        string hostWorkDir,
        string containerImage,
        CancellationToken ct)
    {
        var outputDir = Path.Combine(hostWorkDir, "output");
        Directory.CreateDirectory(outputDir);

        var podmanArgs = $"run --rm --privileged " +
                         $"-v \"{hostWorkDir}:/workspace:Z\" " +
                         $"-w /workspace " +
                         $"{containerImage} " +
                         $"/bin/sh /workspace/build.sh";

        AddLog(job, "info", $"Command: podman {podmanArgs}");

        var psi = new ProcessStartInfo
        {
            FileName = "podman",
            Arguments = podmanArgs,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        try
        {
            using var process = new Process { StartInfo = psi };
            
            process.OutputDataReceived += (s, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    HandleProcessOutputLine(job, e.Data);
                }
            };

            process.ErrorDataReceived += (s, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    HandleProcessOutputLine(job, e.Data, isError: true);
                }
            };

            if (!process.Start())
            {
                return (false, "Failed to start the Podman process.");
            }

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.WaitForExitAsync(ct);

            if (process.ExitCode != 0)
            {
                return (false, $"Podman container exited with error code {process.ExitCode}.");
            }

            return (true, null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return (false, $"Failed to execute Podman: {ex.Message}. Verify that Podman is installed and running.");
        }
    }

    private void HandleProcessOutputLine(BuildJob job, string line, bool isError = false)
    {
        var level = isError ? "warn" : "info";

        if (line.Contains("[STAGE 1/5]", StringComparison.OrdinalIgnoreCase))
        {
            job.ProgressPercent = 30;
            job.Status = BuildStatus.InstallingPackages;
            job.StatusMessage = "Installing ISO build toolchain in worker container...";
            NotifyJobUpdated(job);
        }
        else if (line.Contains("[STAGE 2/5]", StringComparison.OrdinalIgnoreCase))
        {
            job.ProgressPercent = 50;
            job.Status = BuildStatus.InstallingPackages;
            job.StatusMessage = "Bootstrapping target rootfs and installing packages...";
            NotifyJobUpdated(job);
        }
        else if (line.Contains("[STAGE 3/5]", StringComparison.OrdinalIgnoreCase))
        {
            job.ProgressPercent = 65;
            job.Status = BuildStatus.Configuring;
            job.StatusMessage = "Configuring users, credentials, and custom commands...";
            NotifyJobUpdated(job);
        }
        else if (line.Contains("[STAGE 4/5]", StringComparison.OrdinalIgnoreCase))
        {
            job.ProgressPercent = 80;
            job.Status = BuildStatus.CreatingIso;
            job.StatusMessage = "Compressing SquashFS and setting up bootloaders...";
            NotifyJobUpdated(job);
        }
        else if (line.Contains("[STAGE 5/5]", StringComparison.OrdinalIgnoreCase))
        {
            job.ProgressPercent = 90;
            job.Status = BuildStatus.CreatingIso;
            job.StatusMessage = "Generating hybrid bootable ISO with xorriso...";
            NotifyJobUpdated(job);
        }

        AddLog(job, level, line);
    }

    private string GenerateBuildScript(IsoRecipe recipe, string jobId)
    {
        var packagesStr = string.Join(" ", recipe.Packages);
        var isoFileName = $"{recipe.IsoLabel.ToLowerInvariant()}-{jobId}.iso";

        var sb = new StringBuilder();
        sb.AppendLine("#!/bin/sh");
        sb.AppendLine("set -e");
        sb.AppendLine("echo '=================================================='");
        sb.AppendLine("echo '   AI-ISO-Creator: Building Live Linux ISO'");
        sb.AppendLine("echo '=================================================='");
        sb.AppendLine();
        sb.AppendLine("WORKDIR='/workspace'");
        sb.AppendLine("ROOTFS=\"$WORKDIR/rootfs\"");
        sb.AppendLine("ISODIR=\"$WORKDIR/iso\"");
        sb.AppendLine("OUTDIR=\"$WORKDIR/output\"");
        sb.AppendLine();
        sb.AppendLine("mkdir -p \"$ROOTFS\" \"$ISODIR/boot/syslinux\" \"$ISODIR/boot/grub\" \"$OUTDIR\"");
        sb.AppendLine();
        sb.AppendLine("echo '[STAGE 1/5] Installing ISO build toolchain in worker container...'");
        sb.AppendLine("if command -v pacman >/dev/null 2>&1; then");
        sb.AppendLine("    pacman -Sy --noconfirm xorriso syslinux squashfs-tools dosfstools arch-install-scripts");
        sb.AppendLine("    echo '[STAGE 2/5] Bootstrapping target Arch rootfs via pacstrap...'");
        sb.AppendLine($"    pacstrap -K \"$ROOTFS\" base linux linux-firmware {packagesStr} || true");
        sb.AppendLine("elif command -v dnf >/dev/null 2>&1; then");
        sb.AppendLine("    dnf install -y --setopt=install_weak_deps=False xorriso squashfs-tools dosfstools syslinux");
        sb.AppendLine("    echo '[STAGE 2/5] Bootstrapping target rootfs via dnf...'");
        sb.AppendLine($"    dnf --installroot=\"$ROOTFS\" --releasever=40 -y install --setopt=install_weak_deps=False @core kernel {packagesStr} || true");
        sb.AppendLine("elif command -v zypper >/dev/null 2>&1; then");
        sb.AppendLine("    zypper -n install xorriso squashfs syslinux dosfstools");
        sb.AppendLine("    echo '[STAGE 2/5] Bootstrapping target openSUSE rootfs via zypper...'");
        sb.AppendLine($"    zypper --root \"$ROOTFS\" -n install patterns-base-base kernel-default {packagesStr} || true");
        sb.AppendLine("elif command -v apt-get >/dev/null 2>&1; then");
        sb.AppendLine("    apt-get update && apt-get install -y xorriso syslinux-common isolinux squashfs-tools dosfstools debootstrap");
        sb.AppendLine("    echo '[STAGE 2/5] Bootstrapping target rootfs via debootstrap...'");
        sb.AppendLine("    DIST_SUITE=\"bookworm\"");
        sb.AppendLine("    grep -qi ubuntu /etc/os-release && DIST_SUITE=\"noble\"");
        sb.AppendLine("    debootstrap --arch=amd64 $DIST_SUITE \"$ROOTFS\" || true");
        sb.AppendLine($"    chroot \"$ROOTFS\" apt-get update && chroot \"$ROOTFS\" apt-get install -y linux-image-generic linux-image-amd64 {packagesStr} || true");
        sb.AppendLine("else");
        sb.AppendLine("    apk update");
        sb.AppendLine("    apk add --no-cache xorriso syslinux squashfs-tools dosfstools grub-efi efibootmgr mtools bash shadow curl");
        sb.AppendLine("    echo '[STAGE 2/5] Bootstrapping target rootfs via apk...'");
        sb.AppendLine("    apk add --root \"$ROOTFS\" --initdb --repositories-file /etc/apk/repositories \\");
        sb.AppendLine($"        alpine-base alpine-conf linux-lts mkinitfs {packagesStr}");
        sb.AppendLine("fi");
        sb.AppendLine();
        sb.AppendLine("echo '[STAGE 3/5] Configuring target system users and services...'");
        sb.AppendLine($"echo '{recipe.Hostname}' > \"$ROOTFS/etc/hostname\"");
        sb.AppendLine($"echo 'root:{recipe.RootPassword}' | chroot \"$ROOTFS\" chpasswd || true");
        sb.AppendLine();

        if (!string.IsNullOrWhiteSpace(recipe.DefaultUsername))
        {
            sb.AppendLine($"# Create user {recipe.DefaultUsername}");
            sb.AppendLine($"chroot \"$ROOTFS\" useradd -m -s /bin/bash {recipe.DefaultUsername} 2>/dev/null || chroot \"$ROOTFS\" adduser -D -s /bin/bash {recipe.DefaultUsername} || true");
            sb.AppendLine($"echo '{recipe.DefaultUsername}:{recipe.DefaultUserPassword}' | chroot \"$ROOTFS\" chpasswd || true");
            if (recipe.EnableSudo)
            {
                sb.AppendLine("apk add --root \"$ROOTFS\" sudo 2>/dev/null || true");
                sb.AppendLine($"chroot \"$ROOTFS\" usermod -aG wheel {recipe.DefaultUsername} 2>/dev/null || chroot \"$ROOTFS\" addgroup {recipe.DefaultUsername} wheel 2>/dev/null || true");
                sb.AppendLine("echo '%wheel ALL=(ALL) ALL' >> \"$ROOTFS/etc/sudoers\" || true");
            }
        }

        // Enable OpenRC services
        foreach (var svc in recipe.ServicesToEnable)
        {
            sb.AppendLine($"chroot \"$ROOTFS\" rc-update add {svc} default 2>/dev/null || true");
        }

        // Run custom chroot commands
        if (recipe.CustomCommands.Count > 0)
        {
            sb.AppendLine("# Executing user custom commands:");
            foreach (var cmd in recipe.CustomCommands)
            {
                sb.AppendLine($"chroot \"$ROOTFS\" /bin/sh -c \"{cmd.Replace("\"", "\\\"")}\" || true");
            }
        }

        sb.AppendLine();
        sb.AppendLine("echo '[STAGE 4/5] Preparing kernel, initramfs, and SquashFS compression...'");
        sb.AppendLine("# Copy kernel and initramfs to ISO structure");
        sb.AppendLine("cp \"$ROOTFS/boot/vmlinuz-lts\" \"$ISODIR/boot/vmlinuz\" || cp \"$ROOTFS\"/boot/vmlinuz* \"$ISODIR/boot/vmlinuz\" 2>/dev/null || true");
        sb.AppendLine("cp \"$ROOTFS/boot/initramfs-lts\" \"$ISODIR/boot/initramfs\" || cp \"$ROOTFS\"/boot/initramfs* \"$ISODIR/boot/initramfs\" 2>/dev/null || true");
        sb.AppendLine();
        sb.AppendLine("# Compress rootfs into SquashFS");
        sb.AppendLine("mksquashfs \"$ROOTFS\" \"$ISODIR/boot/rootfs.squashfs\" -comp xz -noappend -wildcards -e \"$ROOTFS/boot/*\"");
        sb.AppendLine();
        sb.AppendLine("# Configure ISOLINUX / SYSLINUX for BIOS MBR");
        sb.AppendLine("cat << 'EOF' > \"$ISODIR/boot/syslinux/syslinux.cfg\"");
        sb.AppendLine("DEFAULT linux");
        sb.AppendLine("LABEL linux");
        sb.AppendLine("  SAY Booting AI Linux Live System...");
        sb.AppendLine("  LINUX /boot/vmlinuz");
        sb.AppendLine("  INITRD /boot/initramfs");
        sb.AppendLine($"  APPEND {recipe.KernelParameters} modules=loop,squashfs,sd-mod,usb-storage");
        sb.AppendLine("EOF");
        sb.AppendLine();
        sb.AppendLine("cp /usr/share/syslinux/isolinux.bin \"$ISODIR/boot/syslinux/\" 2>/dev/null || true");
        sb.AppendLine("cp /usr/share/syslinux/ldlinux.c32 \"$ISODIR/boot/syslinux/\" 2>/dev/null || true");
        sb.AppendLine();
        sb.AppendLine("# Configure GRUB for UEFI");
        sb.AppendLine("cat << 'EOF' > \"$ISODIR/boot/grub/grub.cfg\"");
        sb.AppendLine("set default=0");
        sb.AppendLine("set timeout=5");
        sb.AppendLine($"menuentry \"{recipe.IsoLabel} Live\" {{");
        sb.AppendLine("    linux /boot/vmlinuz " + recipe.KernelParameters + " modules=loop,squashfs,sd-mod,usb-storage");
        sb.AppendLine("    initrd /boot/initramfs");
        sb.AppendLine("}");
        sb.AppendLine("EOF");
        sb.AppendLine();
        sb.AppendLine("echo '[STAGE 5/5] Generating bootable hybrid ISO image with xorriso...'");
        sb.AppendLine($"xorriso -as mkisofs -r -V \"{recipe.IsoLabel}\" \\");
        sb.AppendLine("    -J -joliet-long \\");
        sb.AppendLine("    -b boot/syslinux/isolinux.bin -c boot/syslinux/boot.cat \\");
        sb.AppendLine("    -no-emul-boot -boot-load-size 4 -boot-info-table \\");
        sb.AppendLine($"    -o \"$OUTDIR/{isoFileName}\" \"$ISODIR\" || \\");
        sb.AppendLine($"xorriso -as mkisofs -r -V \"{recipe.IsoLabel}\" -o \"$OUTDIR/{isoFileName}\" \"$ISODIR\"");
        sb.AppendLine();
        sb.AppendLine("echo 'Success! ISO image generated:'");
        sb.AppendLine($"ls -lh \"$OUTDIR/{isoFileName}\"");
        sb.AppendLine("echo '=================================================='");

        return sb.ToString();
    }

    private void AddLog(BuildJob job, string level, string message)
    {
        var entry = new BuildLogEntry
        {
            Timestamp = DateTime.UtcNow,
            Level = level,
            Message = message
        };

        lock (job.Logs)
        {
            job.Logs.Add(entry);
        }

        OnLogReceived?.Invoke(job.Id, entry);
    }

    private void NotifyJobUpdated(BuildJob job)
    {
        OnJobUpdated?.Invoke(job.Id, job);
    }

    private static async Task<string> ComputeSha256Async(string filePath, CancellationToken ct)
    {
        using var sha256 = SHA256.Create();
        await using var stream = File.OpenRead(filePath);
        var hashBytes = await sha256.ComputeHashAsync(stream, ct);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    private static string FormatBytes(long bytes)
    {
        string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
        int counter = 0;
        decimal number = bytes;
        while (Math.Round(number / 1024) >= 1)
        {
            number /= 1024;
            counter++;
        }
        return $"{number:n1} {suffixes[counter]}";
    }
}
