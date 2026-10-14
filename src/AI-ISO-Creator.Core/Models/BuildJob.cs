namespace AI_ISO_Creator.Core.Models;

/// <summary>
/// Status enumeration for an ISO build lifecycle.
/// </summary>
public enum BuildStatus
{
    Queued,
    Initializing,
    InstallingPackages,
    Configuring,
    CreatingIso,
    Completed,
    Failed,
    Cancelled
}

/// <summary>
/// Represents an individual log entry emitted during the build process.
/// </summary>
public class BuildLogEntry
{
    /// <summary>
    /// UTC timestamp of the log event.
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Severity level of the entry: "info", "warn", "error", or "success".
    /// </summary>
    public string Level { get; set; } = "info";

    /// <summary>
    /// Log message content.
    /// </summary>
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// Represents a containerized ISO build task and its associated state.
/// </summary>
public class BuildJob
{
    /// <summary>
    /// Unique identifier for the build task.
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];

    /// <summary>
    /// Recipe specification used to produce this ISO.
    /// </summary>
    public IsoRecipe Recipe { get; set; } = new();

    /// <summary>
    /// Current execution status of the job.
    /// </summary>
    public BuildStatus Status { get; set; } = BuildStatus.Queued;

    /// <summary>
    /// Human-readable status description for UI display.
    /// </summary>
    public string StatusMessage { get; set; } = "Waiting in build queue...";

    /// <summary>
    /// Estimated progress percentage (0 - 100).
    /// </summary>
    public int ProgressPercent { get; set; } = 0;

    /// <summary>
    /// Absolute host filesystem path to the generated ISO image.
    /// </summary>
    public string? OutputIsoPath { get; set; }

    /// <summary>
    /// Filename of the resulting ISO artifact.
    /// </summary>
    public string? OutputIsoFileName { get; set; }

    /// <summary>
    /// Size of the resulting ISO file in bytes.
    /// </summary>
    public long OutputIsoSize { get; set; } = 0;

    /// <summary>
    /// SHA-256 cryptographic hash of the generated ISO image.
    /// </summary>
    public string? OutputIsoSha256 { get; set; }

    /// <summary>
    /// UTC timestamp when the job was queued.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// UTC timestamp when container build execution began.
    /// </summary>
    public DateTime? StartedAt { get; set; }

    /// <summary>
    /// UTC timestamp when the job reached a terminal state (Completed/Failed/Cancelled).
    /// </summary>
    public DateTime? FinishedAt { get; set; }

    /// <summary>
    /// Detailed error message if the build failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Historical console and diagnostic log entries.
    /// </summary>
    public List<BuildLogEntry> Logs { get; set; } = new();
}
