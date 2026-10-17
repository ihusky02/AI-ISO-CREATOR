using System.Text;

namespace AI_ISO_Creator.Core.Services.UploadedIsoModules;

internal abstract class UploadedIsoDistroModule : IUploadedIsoDistroModule
{
    public abstract string[] DistributionIds { get; }
    public abstract string PackageManagerUpdateCommand { get; }
    public abstract string PackageManagerInstallCommand { get; }
    public abstract string PackageManagerRemoveCommand { get; }
    public abstract string ServiceManager { get; }
    public abstract IReadOnlyDictionary<string, (string Packages, string DisplayManager)> Desktops { get; }
    public abstract string ConfigureRepositoriesScript { get; }

    public virtual string MapPackage(string package) => package.ToLowerInvariant() switch
    {
        "photorec" => "testdisk",
        _ => package
    };

    public void AppendSelectionCase(StringBuilder script)
    {
        script.AppendLine($"    {string.Join("|", DistributionIds)})");
        foreach (var line in ConfigureRepositoriesScript.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            script.AppendLine("        " + line);
        }

        script.AppendLine($"        PACKAGE_MANAGER_UPDATE='{PackageManagerUpdateCommand}'");
        script.AppendLine($"        PACKAGE_MANAGER_INSTALL='{PackageManagerInstallCommand}'");
        script.AppendLine($"        PACKAGE_MANAGER_REMOVE='{PackageManagerRemoveCommand}'");
        script.AppendLine($"        SERVICE_MANAGER='{ServiceManager}'");
        script.AppendLine("        case \"$DESKTOP_ENVIRONMENT\" in");
        foreach (var (desktop, configuration) in Desktops)
        {
            script.AppendLine(
                $"            {desktop}) DESKTOP_PACKAGES='{configuration.Packages}'; DISPLAY_MANAGER='{configuration.DisplayManager}' ;;");
        }

        script.AppendLine(
            "            *) echo \"Desktop '$DESKTOP_ENVIRONMENT' is not supported for '$DISTRO_ID'.\" >&2; exit 2 ;;");
        script.AppendLine("        esac");
        script.AppendLine("        ;;");
    }
}