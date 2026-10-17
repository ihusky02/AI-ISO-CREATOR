namespace AI_ISO_Creator.Core.Services.UploadedIsoModules;

internal sealed class DebianUbuntuModule : UploadedIsoDistroModule
{
    public override string[] DistributionIds => ["ubuntu", "debian", "zorin"];
    public override string PackageManagerUpdateCommand =>
        "apt-get update -o Acquire::Retries=3 -o Acquire::AllowInsecureRepositories=true -o Acquire::AllowDowngradeToInsecureRepositories=true -o APT::Get::AllowUnauthenticated=true";
    public override string PackageManagerInstallCommand =>
        "apt-get install -y --no-install-recommends --allow-unauthenticated -o APT::Get::AllowUnauthenticated=true";
    public override string PackageManagerRemoveCommand => "apt-get remove -y --purge";
    public override string ServiceManager => "systemd";

    public override IReadOnlyDictionary<string, (string Packages, string DisplayManager)> Desktops =>
        new Dictionary<string, (string, string)>
        {
            ["none"] = ("", ""),
            ["xfce"] = ("xfce4 lightdm", "lightdm.service"),
            ["gnome"] = ("gnome-shell gdm3", "gdm3.service"),
            ["kde"] = ("plasma-desktop sddm", "sddm.service"),
            ["i3"] = ("i3 lightdm", "lightdm.service")
        };

    public override string ConfigureRepositoriesScript => """
                                                          if [ "$DISTRO_ID" = ubuntu ] || [ "$DISTRO_ID" = zorin ]; then
                                                              for source in "$ROOTFS"/etc/apt/sources.list "$ROOTFS"/etc/apt/sources.list.d/*.list; do
                                                                  [ -f "$source" ] || continue
                                                                  sed -i -E '/^[[:space:]]*deb(-src)?[[:space:]]/ { /restricted/! s/( main)([[:space:]]|$)/\1 restricted\2/; /universe/! s/( main)([[:space:]]|$)/\1 universe\2/; /multiverse/! s/( main)([[:space:]]|$)/\1 multiverse\2/; }' "$source"
                                                              done
                                                              for source in "$ROOTFS"/etc/apt/sources.list.d/*.sources; do
                                                                  [ -f "$source" ] || continue
                                                                  sed -i -E '/^Components:/ { /(^|[[:space:]])restricted([[:space:]]|$)/! s/$/ restricted/; /(^|[[:space:]])universe([[:space:]]|$)/! s/$/ universe/; /(^|[[:space:]])multiverse([[:space:]]|$)/! s/$/ multiverse/; }' "$source"
                                                              done
                                                          else
                                                              DEBIAN_RELEASE=$(sed -n 's/^VERSION_CODENAME=//p' "$ROOTFS/etc/os-release" | head -n 1 | tr -d '"')
                                                              for source in "$ROOTFS"/etc/apt/sources.list "$ROOTFS"/etc/apt/sources.list.d/*.list; do
                                                                  [ -f "$source" ] || continue
                                                                  sed -i -E '/^[[:space:]]*deb(-src)?[[:space:]]/ { /contrib/! s/( main)([[:space:]]|$)/\1 contrib\2/; /(^|[[:space:]])non-free([[:space:]]|$)/! s/( main)([[:space:]]|$)/\1 non-free\2/; }' "$source"
                                                                  case "$DEBIAN_RELEASE" in
                                                                      bookworm|trixie|forky)
                                                                          sed -i -E '/^[[:space:]]*deb(-src)?[[:space:]]/ { /non-free-firmware/! s/( main)([[:space:]]|$)/\1 non-free-firmware\2/; }' "$source"
                                                                          ;;
                                                                  esac
                                                              done
                                                              for source in "$ROOTFS"/etc/apt/sources.list.d/*.sources; do
                                                                  [ -f "$source" ] || continue
                                                                  sed -i -E '/^Components:/ { /(^|[[:space:]])contrib([[:space:]]|$)/! s/$/ contrib/; /(^|[[:space:]])non-free([[:space:]]|$)/! s/$/ non-free/; }' "$source"
                                                                  case "$DEBIAN_RELEASE" in
                                                                      bookworm|trixie|forky)
                                                                          sed -i -E '/^Components:/ { /(^|[[:space:]])non-free-firmware([[:space:]]|$)/! s/$/ non-free-firmware/; }' "$source"
                                                                          ;;
                                                                  esac
                                                              done
                                                          fi
                                                          """;

    public override string MapPackage(string package) => package.ToLowerInvariant() switch
    {
        "ddrescue" => "gddrescue",
        _ => base.MapPackage(package)
    };
}