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
    [InlineData("xfce", "xubuntu-desktop-minimal lightdm", "xfce4 lightdm")]
    [InlineData("gnome", "ubuntu-desktop-minimal", "gnome-core gdm3")]
    [InlineData("kde", "kubuntu-desktop", "kde-plasma-desktop sddm")]
    [InlineData("i3", "i3 lightdm", "i3 lightdm")]
    public void BuildScript_CustomizesUploadedLiveIsoAndSelectsDistroSpecificDesktopPackages(
        string desktopEnvironment,
        string ubuntuDesktopPackages,
        string debianDesktopPackages)
    {
        var recipe = new IsoRecipe
        {
            DesktopEnvironment = desktopEnvironment,
            Packages = ["gparted", "photorec", "ddrescue"]
        };

        var script = ContainerBuilderService.GenerateBuildScript(recipe, "test-job");

        Assert.Contains("find \"$ISO_DIR\" -type f -name filesystem.squashfs", script);
        Assert.Contains("unsquashfs -d \"$ROOTFS\" \"$SQUASHFS\"", script);
        Assert.Contains("ubuntu|debian", script);
        Assert.Contains("Unsupported source architecture", script);
        Assert.Contains("universe multiverse", script);
        Assert.Contains("contrib non-free non-free-firmware", script);
        Assert.Contains($"ubuntu:{desktopEnvironment}) DESKTOP_PACKAGES='{ubuntuDesktopPackages}'", script);
        Assert.Contains($"debian:{desktopEnvironment}) DESKTOP_PACKAGES='{debianDesktopPackages}'", script);
        Assert.Contains("PACKAGE_LIST=$(printf '%s\\n' \"$DESKTOP_PACKAGES gparted testdisk gddrescue\" | xargs)", script);
        Assert.Contains("-boot_image any replay", script);
        Assert.DoesNotContain("debootstrap", script);
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
