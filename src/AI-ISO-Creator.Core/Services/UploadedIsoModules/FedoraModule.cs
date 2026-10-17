namespace AI_ISO_Creator.Core.Services.UploadedIsoModules;

internal sealed class FedoraModule : UploadedIsoDistroModule
{
    public override string[] DistributionIds => ["fedora"];
    public override string PackageManagerUpdateCommand => "dnf makecache --refresh";
    public override string PackageManagerInstallCommand => "dnf install -y";
    public override string PackageManagerRemoveCommand => "dnf remove -y";
    public override string ServiceManager => "systemd";
    public override string ConfigureRepositoriesScript => "true";

    public override IReadOnlyDictionary<string, (string Packages, string DisplayManager)> Desktops =>
        new Dictionary<string, (string, string)>
        {
            ["none"] = ("", ""),
            ["xfce"] = ("xfce4-session xfce4-panel xfce4-terminal lightdm lightdm-gtk", "lightdm.service"),
            ["gnome"] = ("gnome-shell gdm gnome-terminal", "gdm.service"),
            ["kde"] = ("plasma-workspace sddm", "sddm.service"),
            ["i3"] = ("i3 i3status dmenu lightdm lightdm-gtk", "lightdm.service")
        };

    public override string MapPackage(string package) => package.ToLowerInvariant() switch
    {
        "build-essential" => "@development-tools",
        "build-base" => "@development-tools",
        "g++" => "gcc-c++",
        "gddrescue" => "ddrescue",
        "openssh" => "openssh-server",
        "tshark" => "wireshark-cli",
        "dnsutils" => "bind-utils",
        _ => base.MapPackage(package)
    };
}