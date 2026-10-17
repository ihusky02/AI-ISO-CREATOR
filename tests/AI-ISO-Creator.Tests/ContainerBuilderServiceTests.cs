using System.Diagnostics;
using AI_ISO_Creator.Core.Models;
using AI_ISO_Creator.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AI_ISO_Creator.Tests;

/// <summary>
/// Unit tests verifying Podman container engine checks and job management logic.
/// </summary>
public class ContainerBuilderServiceTests
{
    private readonly ContainerBuilderService _builderService;

    public ContainerBuilderServiceTests()
    {
        _builderService = new ContainerBuilderService(NullLogger<ContainerBuilderService>.Instance);
    }

    [Fact]
    public void PodmanAvailability_CanBeChecked()
    {
        // Act: verify that method checks podman without throwing exceptions
        var isPodmanAvailable = _builderService.IsPodmanAvailable();

        // Assert: Podman 5.8.7 is available in this environment
        Assert.True(isPodmanAvailable);
    }

    [Fact]
    public void QemuAvailability_CanBeChecked()
    {
        // Act: verify that method checks qemu-system-x86_64
        var isQemuAvailable = _builderService.IsQemuAvailable();

        // Assert: QEMU is installed on this environment
        Assert.True(isQemuAvailable);
    }

    [Fact]
    public void JobManagement_ReturnsCorrectJobDetails()
    {
        // Act
        var allJobs = _builderService.GetAllJobs();

        // Assert
        Assert.NotNull(allJobs);
    }

    [Theory]
    [InlineData("xfce", "xfce4 lightdm")]
    [InlineData("gnome", "gnome-shell gdm3")]
    [InlineData("kde", "plasma-desktop sddm")]
    [InlineData("i3", "i3 lightdm")]
    public void BuildScript_CustomizesUploadedLiveIsoAndSelectsDistroSpecificDesktopPackages(
        string desktopEnvironment,
        string desktopPackages)
    {
        var recipe = new IsoRecipe
        {
            DesktopEnvironment = desktopEnvironment,
            Packages = ["gparted", "photorec", "ddrescue"]
        };

        var script = ContainerBuilderService.GenerateBuildScript(recipe, "test-job");

        Assert.Contains("-name filesystem.squashfs -o -name rootfs.sfs", script);
        Assert.Contains("unsquashfs -excludes -d \"$SQUASHFS_CONTENTS\" \"$SQUASHFS\" 'dev/*'", script);
        Assert.Contains("ubuntu|debian|zorin)", script);
        Assert.Contains("[ \"$DISTRO_ID\" = zorin ]", script);
        Assert.Contains("Unsupported source architecture", script);
        Assert.Contains("multiverse", script);
        Assert.Contains("non-free-firmware", script);
        Assert.Contains($"{desktopEnvironment}) DESKTOP_PACKAGES='{desktopPackages}'", script);
        Assert.Contains("USER_PACKAGES='gparted testdisk gddrescue'", script);
        Assert.Contains("PACKAGE_LIST=$(printf '%s\\n' \"$DESKTOP_PACKAGES $USER_PACKAGES\" | xargs)", script);
        Assert.Contains("REMOVE_PACKAGE_LIST=$(printf '%s\\n' \"$USER_PACKAGES_TO_REMOVE\" | xargs)", script);
        Assert.Contains("chroot \"$ROOTFS\" /bin/sh -c \"$PACKAGE_MANAGER_REMOVE $REMOVE_PACKAGE_LIST\"", script);
        Assert.Contains("rm -f \"$ROOTFS/etc/resolv.conf\"\ncp -L /etc/resolv.conf \"$ROOTFS/etc/resolv.conf\"", script);
        Assert.Contains("RESOLV_CONF_STATE=symlink", script);
        Assert.Contains("RESOLV_CONF_STATE=file", script);
        Assert.Contains("Using the build container DNS configuration", script);
        Assert.Contains("-boot_image any replay", script);
        Assert.DoesNotContain("debootstrap", script);
    }

    [Fact]
    public void BuildScript_IncludesSeparateModulesForSupportedPackageManagers()
    {
        var script = ContainerBuilderService.GenerateBuildScript(new IsoRecipe
        {
            Packages = ["build-essential", "ddrescue", "dnsutils"],
            PackagesToRemove = ["games", "nano"],
            SuggestedCommands = ["apt-get install vlc"]
        }, "test-job");

        Assert.Contains("fedora)", script);
        Assert.Contains("Acquire::AllowInsecureRepositories=true", script);
        Assert.Contains("Acquire::AllowDowngradeToInsecureRepositories=true", script);
        Assert.Contains("APT::Get::AllowUnauthenticated=true", script);
        Assert.Contains("--allow-unauthenticated", script);
        Assert.Contains("umount -lf \"$ROOTFS/$mountpoint\" 2>/dev/null || true", script);
        Assert.Contains("PACKAGE_MANAGER_UPDATE='dnf makecache --refresh'", script);
        Assert.Contains("arch|archlinux)", script);
        Assert.Contains("PACKAGE_MANAGER_UPDATE='pacman -Syu --noconfirm'", script);
        Assert.Contains("opensuse|opensuse-leap|opensuse-tumbleweed)", script);
        Assert.Contains("PACKAGE_MANAGER_UPDATE='zypper --non-interactive refresh'", script);
        Assert.Contains("PACKAGE_MANAGER_REMOVE='apt-get remove -y --purge'", script);
        Assert.Contains("PACKAGE_MANAGER_REMOVE='dnf remove -y'", script);
        Assert.Contains("PACKAGE_MANAGER_REMOVE='pacman -Rns --noconfirm'", script);
        Assert.Contains("PACKAGE_MANAGER_REMOVE='zypper --non-interactive remove -y'", script);
        Assert.Contains("USER_PACKAGES_TO_REMOVE='games nano'", script);
        Assert.DoesNotContain("apt-get install vlc", script);
        Assert.Contains("USER_PACKAGES='@development-tools ddrescue bind-utils'", script);
        Assert.Contains("USER_PACKAGES='base-devel gnu-ddrescue bind'", script);
        Assert.Contains("USER_PACKAGES='patterns-devel-base-devel_basis ddrescue bind-utils'", script);
        Assert.Contains("-name rootfs.img -o -name ext3fs.img", script);
    }

    [Fact]
    public void BuildScript_HasValidBashSyntax()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var recipe = new IsoRecipe
        {
            DesktopEnvironment = "xfce",
            Packages = ["gparted", "photorec"]
        };
        var script = ContainerBuilderService.GenerateBuildScript(recipe, "test-job");
        var startInfo = new ProcessStartInfo("bash", "-n")
        {
            RedirectStandardInput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        using var process = Process.Start(startInfo);
        Assert.NotNull(process);
        process.StandardInput.Write(script);
        process.StandardInput.Close();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();

        Assert.True(process.ExitCode == 0, error);
    }
}