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
        var applicationDataDirectory = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(applicationDataDirectory))
        {
            applicationDataDirectory = Path.GetTempPath();
        }

        _buildsRootDirectory = Path.Combine(applicationDataDirectory, "AI-ISO-Creator", "build_artifacts");
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

    public async Task<BuildJob> StartBuildAsync(
        IsoRecipe recipe,
        Stream sourceIso,
        string sourceIsoFileName,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        ArgumentNullException.ThrowIfNull(sourceIso);
        if (!Path.GetExtension(sourceIsoFileName).Equals(".iso", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("The uploaded source file must have an .iso extension.",
                nameof(sourceIsoFileName));
        }

        ValidateRecipe(recipe);
        var job = new BuildJob
        {
            Id = Guid.NewGuid().ToString("N")[..8],
            Recipe = recipe,
            SourceIsoFileName = Path.GetFileName(sourceIsoFileName),
            Status = BuildStatus.Queued,
            StatusMessage = "Build task queued...",
            ProgressPercent = 5,
            CreatedAt = DateTime.UtcNow
        };

        var jobWorkDir = Path.Combine(_buildsRootDirectory, job.Id);
        Directory.CreateDirectory(jobWorkDir);
        try
        {
            await using var sourceFile = new FileStream(
                Path.Combine(jobWorkDir, "source.iso"),
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                1024 * 1024,
                useAsync: true);
            await sourceIso.CopyToAsync(sourceFile, cancellationToken);
            await sourceFile.FlushAsync(cancellationToken);
        }
        catch
        {
            Directory.Delete(jobWorkDir, recursive: true);
            throw;
        }

        _jobs[job.Id] = job;
        var jobCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _cancellationTokens[job.Id] = jobCts;

        AddLog(job, "info", $"Started ISO customization job [{job.Id}] from {job.SourceIsoFileName}.");
        NotifyJobUpdated(job);

        _ = Task.Run(() => ExecuteBuildProcessAsync(job, jobCts.Token), jobCts.Token);

        return job;
    }

    private static void ValidateRecipe(IsoRecipe recipe)
    {
        static bool HasInvalidPackageNames(IEnumerable<string>? packages) =>
            packages == null || packages.Count() > 100 || packages.Any(package =>
                string.IsNullOrWhiteSpace(package) ||
                !System.Text.RegularExpressions.Regex.IsMatch(package, @"^[a-zA-Z0-9][a-zA-Z0-9.+-]*$"));

        if (HasInvalidPackageNames(recipe.Packages) || HasInvalidPackageNames(recipe.PackagesToRemove))
        {
            throw new ArgumentException("Package names may contain only letters, numbers, '.', '+', and '-'.");
        }

        if (recipe.Packages.Intersect(recipe.PackagesToRemove, StringComparer.OrdinalIgnoreCase).Any())
        {
            throw new ArgumentException("A package cannot be both installed and removed in the same build.");
        }

        if (recipe.SuggestedCommands is null || recipe.SuggestedCommands.Count > 20 ||
            recipe.SuggestedCommands.Any(command => string.IsNullOrWhiteSpace(command) || command.Length > 300))
        {
            throw new ArgumentException("Suggested commands may contain at most 20 non-empty entries of 300 characters each.");
        }

        if (recipe.DesktopEnvironment is not ("none" or "xfce" or "gnome" or "kde" or "i3"))
        {
            throw new ArgumentException("Choose a supported desktop environment: none, xfce, gnome, kde, or i3.");
        }

        if (string.IsNullOrWhiteSpace(recipe.IsoLabel) ||
            !System.Text.RegularExpressions.Regex.IsMatch(recipe.IsoLabel, @"^[A-Za-z0-9_-]{1,32}$"))
        {
            throw new ArgumentException("The ISO label must contain 1-32 letters, numbers, underscores, or hyphens.");
        }
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
                File.SetUnixFileMode(scriptPath, UnixFileMode.UserRead | UnixFileMode.UserWrite |
                                                 UnixFileMode.UserExecute |
                                                 UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                                                 UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
            }

            job.Status = BuildStatus.InstallingPackages;
            job.StatusMessage = "Starting Podman container to customize the uploaded ISO...";
            job.ProgressPercent = 25;
            AddLog(job, "info", "Starting build environment inside Podman container...");
            NotifyJobUpdated(job);

            var isoOutputFileName = $"{job.Recipe.IsoLabel.ToLowerInvariant()}-{job.Id}.iso";
            var (success, errorMsg) = await RunPodmanProcessAsync(job, jobWorkDir, "fedora:latest", ct);

            if (!success)
            {
                throw new InvalidOperationException(errorMsg ?? "Unknown error occurred inside the build container.");
            }

            var resultingIsoPath = Path.Combine(jobWorkDir, "output", isoOutputFileName);
            if (!File.Exists(resultingIsoPath))
            {
                var outputDir = Path.Combine(jobWorkDir, "output");
                var isoFiles = Directory.Exists(outputDir)
                    ? Directory.GetFiles(outputDir, "*.iso")
                    : Array.Empty<string>();
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
        var errorLines = new ConcurrentQueue<string>();

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
                    errorLines.Enqueue(e.Data);
                    while (errorLines.Count > 20)
                    {
                        errorLines.TryDequeue(out _);
                    }

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
                var errorOutput = string.Join(Environment.NewLine, errorLines);
                var details = string.IsNullOrWhiteSpace(errorOutput)
                    ? string.Empty
                    : $" Last container errors:{Environment.NewLine}{errorOutput}";
                return (false, $"Podman container exited with error code {process.ExitCode}.{details}");
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
            job.StatusMessage = "Extracting the uploaded live ISO and its filesystem...";
            NotifyJobUpdated(job);
        }
        else if (line.Contains("[STAGE 3/5]", StringComparison.OrdinalIgnoreCase))
        {
            job.ProgressPercent = 65;
            job.Status = BuildStatus.InstallingPackages;
            job.StatusMessage = "Enabling repositories and installing the selected desktop and applications...";
            NotifyJobUpdated(job);
        }
        else if (line.Contains("[STAGE 4/5]", StringComparison.OrdinalIgnoreCase))
        {
            job.ProgressPercent = 80;
            job.Status = BuildStatus.CreatingIso;
            job.StatusMessage = "Rebuilding the live filesystem image...";
            NotifyJobUpdated(job);
        }
        else if (line.Contains("[STAGE 5/5]", StringComparison.OrdinalIgnoreCase))
        {
            job.ProgressPercent = 90;
            job.Status = BuildStatus.CreatingIso;
            job.StatusMessage = "Rebuilding the ISO and preserving its boot metadata...";
            NotifyJobUpdated(job);
        }

        AddLog(job, level, line);
    }

    internal static string GenerateBuildScript(IsoRecipe recipe, string jobId) =>
        UploadedIsoBuildScript.Generate(recipe, jobId);

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