using AI_ISO_Creator.Core.Models;

namespace AI_ISO_Creator.Core.Services;

/// <summary>
/// Interface for orchestrating containerized Linux ISO generation and lifecycle management.
/// </summary>
public interface IIsoBuilderService
{
    /// <summary>
    /// Enqueues and initiates an asynchronous containerized build job from the provided recipe.
    /// </summary>
    Task<BuildJob> StartBuildAsync(
        IsoRecipe recipe,
        Stream sourceIso,
        string sourceIsoFileName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a build job by its identifier.
    /// </summary>
    BuildJob? GetJob(string jobId);

    /// <summary>
    /// Returns all registered build jobs in descending order of creation time.
    /// </summary>
    IEnumerable<BuildJob> GetAllJobs();

    /// <summary>
    /// Cancels an in-flight build job.
    /// </summary>
    bool CancelJob(string jobId);

    /// <summary>
    /// Removes a job record and purges its workspace artifacts from disk.
    /// </summary>
    bool DeleteJob(string jobId);

    /// <summary>
    /// Launches the completed ISO image inside a QEMU virtual machine.
    /// </summary>
    Task<bool> LaunchInQemuAsync(string jobId);

    /// <summary>
    /// Checks whether QEMU (qemu-system-x86_64) is available on the host system.
    /// </summary>
    bool IsQemuAvailable();

    /// <summary>
    /// Checks whether the Podman container runtime is available on the host system.
    /// </summary>
    bool IsPodmanAvailable();

    /// <summary>
    /// Event fired whenever a new console or diagnostic log entry is produced.
    /// </summary>
    event Action<string, BuildLogEntry>? OnLogReceived;

    /// <summary>
    /// Event fired whenever a job changes progress or execution status.
    /// </summary>
    event Action<string, BuildJob>? OnJobUpdated;
}
