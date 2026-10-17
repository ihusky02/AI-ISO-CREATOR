using System.Text;

namespace AI_ISO_Creator.Core.Services.UploadedIsoModules;

internal interface IUploadedIsoDistroModule
{
    string[] DistributionIds { get; }
    string PackageManagerUpdateCommand { get; }
    string PackageManagerInstallCommand { get; }
    string PackageManagerRemoveCommand { get; }
    string ServiceManager { get; }
    IReadOnlyDictionary<string, (string Packages, string DisplayManager)> Desktops { get; }
    string ConfigureRepositoriesScript { get; }
    string MapPackage(string package);

    void AppendSelectionCase(StringBuilder script);
}