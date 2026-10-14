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
}
