namespace AI_ISO_Creator.Core.Services.UploadedIsoModules;

internal sealed class OpenSuseModule : UploadedIsoDistroModule
{
    public override string[] DistributionIds => ["opensuse", "opensuse-leap", "opensuse-tumbleweed"];
    public override string PackageManagerUpdateCommand => "zypper --non-interactive refresh";
    public override string PackageManagerInstallCommand => "zypper --non-interactive install --no-recommends -y";
    public override string PackageManagerRemoveCommand => "zypper --non-interactive remove -y";
    public override string ServiceManager => "systemd";

    public override string ConfigureRepositoriesScript =>
        "chroot \"$ROOTFS\" zypper --non-interactive modifyrepo --all --enable";

    public override IReadOnlyDictionary<string, (string Packages, string DisplayManager)> Desktops =>
        new Dictionary<string, (string, string)>
        {
            ["none"] = ("", ""),
            ["xfce"] = ("patterns-xfce-xfce lightdm", "lightdm.service"),
            ["gnome"] = ("patterns-gnome-gnome gdm", "gdm.service"),
            ["kde"] = ("patterns-kde-kde_plasma sddm", "sddm.service"),
            ["i3"] = ("i3 lightdm", "lightdm.service")
        };

    public override string MapPackage(string package) => package.ToLowerInvariant() switch
    {
        "build-essential" => "patterns-devel-base-devel_basis",
        "build-base" => "patterns-devel-base-devel_basis",
        "g++" => "gcc-c++",
        "ddrescue" => "ddrescue",
        "gddrescue" => "ddrescue",
        "openssh-server" => "openssh",
        "tshark" => "wireshark",
        "dnsutils" => "bind-utils",
        _ => base.MapPackage(package)
    };
}