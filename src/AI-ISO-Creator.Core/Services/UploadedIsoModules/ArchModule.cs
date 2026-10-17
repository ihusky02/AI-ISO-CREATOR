namespace AI_ISO_Creator.Core.Services.UploadedIsoModules;

internal sealed class ArchModule : UploadedIsoDistroModule
{
    public override string[] DistributionIds => ["arch", "archlinux"];
    public override string PackageManagerUpdateCommand => "pacman -Syu --noconfirm";
    public override string PackageManagerInstallCommand => "pacman -S --noconfirm --needed";
    public override string PackageManagerRemoveCommand => "pacman -Rns --noconfirm";
    public override string ServiceManager => "systemd";

    public override string ConfigureRepositoriesScript => """
                                                          if [ -f "$ROOTFS/etc/pacman.conf" ]; then
                                                              sed -i -E '/^[[:space:]]*#\[multilib\]/s/^[[:space:]]*#//; /^[[:space:]]*#Include[[:space:]]*=[[:space:]]*\/etc\/pacman.d\/mirrorlist/s/^[[:space:]]*#//' "$ROOTFS/etc/pacman.conf"
                                                          fi
                                                          """;

    public override IReadOnlyDictionary<string, (string Packages, string DisplayManager)> Desktops =>
        new Dictionary<string, (string, string)>
        {
            ["none"] = ("", ""),
            ["xfce"] = ("xfce4 xfce4-goodies lightdm lightdm-gtk-greeter", "lightdm.service"),
            ["gnome"] = ("gnome-shell gdm gnome-terminal nautilus", "gdm.service"),
            ["kde"] = ("plasma-desktop sddm", "sddm.service"),
            ["i3"] = ("i3-wm i3status dmenu lightdm lightdm-gtk-greeter", "lightdm.service")
        };

    public override string MapPackage(string package) => package.ToLowerInvariant() switch
    {
        "build-essential" => "base-devel",
        "build-base" => "base-devel",
        "g++" => "gcc",
        "ddrescue" => "gnu-ddrescue",
        "gddrescue" => "gnu-ddrescue",
        "openssh-server" => "openssh",
        "tshark" => "wireshark-cli",
        "dnsutils" => "bind",
        _ => base.MapPackage(package)
    };
}