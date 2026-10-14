namespace AI_ISO_Creator.Core.Models;

/// <summary>
/// Represents the complete specification for building a custom Linux ISO image.
/// This model is generated and refined by the Gemini AI service or customized by the user.
/// </summary>
public class IsoRecipe
{
    /// <summary>
    /// The target Linux distribution (e.g., "alpine" or "debian").
    /// </summary>
    public string Distribution { get; set; } = "alpine";

    /// <summary>
    /// The release or version identifier (e.g., "3.20" for Alpine, "bookworm" for Debian).
    /// </summary>
    public string Release { get; set; } = "3.20";

    /// <summary>
    /// Target CPU architecture (typically "x86_64").
    /// </summary>
    public string Arch { get; set; } = "x86_64";

    /// <summary>
    /// Volume label assigned to the resulting ISO image.
    /// </summary>
    public string IsoLabel { get; set; } = "AI_LINUX";

    /// <summary>
    /// System hostname configured during bootstrap.
    /// </summary>
    public string Hostname { get; set; } = "ailinux";

    /// <summary>
    /// Password for the root user.
    /// </summary>
    public string RootPassword { get; set; } = "root";

    /// <summary>
    /// Default standard user account name.
    /// </summary>
    public string DefaultUsername { get; set; } = "user";

    /// <summary>
    /// Password for the default standard user account.
    /// </summary>
    public string DefaultUserPassword { get; set; } = "live";

    /// <summary>
    /// Whether the default user should have sudo/wheel administrative permissions.
    /// </summary>
    public bool EnableSudo { get; set; } = true;

    /// <summary>
    /// Optional graphical desktop environment (e.g., "none", "xfce", "i3").
    /// </summary>
    public string DesktopEnvironment { get; set; } = "none";

    /// <summary>
    /// List of packages to install via the distribution's package manager.
    /// </summary>
    public List<string> Packages { get; set; } = new();

    /// <summary>
    /// System services to enable during boot (e.g., "sshd", "networking").
    /// </summary>
    public List<string> ServicesToEnable { get; set; } = new();

    /// <summary>
    /// Optional custom shell commands executed during post-install chroot.
    /// </summary>
    public List<string> CustomCommands { get; set; } = new();

    /// <summary>
    /// Kernel command-line parameters appended to the bootloader configuration.
    /// </summary>
    public string KernelParameters { get; set; } = "quiet console=tty0";

    /// <summary>
    /// Human-readable explanation and rationale produced by the AI model.
    /// </summary>
    public string Summary { get; set; } = string.Empty;

    /// <summary>
    /// Concise description of the intended use case.
    /// </summary>
    public string TargetUseCase { get; set; } = string.Empty;
}
