using System.Text;
using AI_ISO_Creator.Core.Models;

namespace AI_ISO_Creator.Core.Services;

internal static class UploadedIsoBuildScript
{
    public static string Generate(IsoRecipe recipe, string jobId)
    {
        var packages = recipe.Packages
            .Select(package => package.ToLowerInvariant() switch
            {
                "photorec" => "testdisk",
                "ddrescue" => "gddrescue",
                _ => package
            })
            .Distinct(StringComparer.OrdinalIgnoreCase);
        var packageList = string.Join(" ", packages);
        var outputFileName = $"{recipe.IsoLabel.ToLowerInvariant()}-{jobId}.iso";
        var script = new StringBuilder();

        script.AppendLine("#!/bin/bash");
        script.AppendLine("set -Eeuo pipefail");
        script.AppendLine("WORKDIR=/workspace");
        script.AppendLine("SOURCE_ISO=\"$WORKDIR/source.iso\"");
        script.AppendLine("ISO_DIR=\"$WORKDIR/iso\"");
        script.AppendLine("ROOTFS=\"$WORKDIR/rootfs\"");
        script.AppendLine("OUT_DIR=\"$WORKDIR/output\"");
        script.AppendLine("OUTPUT_ISO=\"$OUT_DIR/" + outputFileName + "\"");
        script.AppendLine("cleanup() {");
        script.AppendLine("    for mountpoint in run sys dev/pts dev proc; do");
        script.AppendLine("        mountpoint -q \"$ROOTFS/$mountpoint\" && umount -lf \"$ROOTFS/$mountpoint\" || true");
        script.AppendLine("    done");
        script.AppendLine("}");
        script.AppendLine("trap cleanup EXIT");
        script.AppendLine("mkdir -p \"$ISO_DIR\" \"$ROOTFS\" \"$OUT_DIR\"");
        script.AppendLine("echo '[STAGE 1/5] Installing ISO repackaging tools...'");
        script.AppendLine("dnf install -y xorriso squashfs-tools util-linux coreutils findutils curl gnupg2 file");
        script.AppendLine("echo '[STAGE 2/5] Extracting the uploaded live ISO...'");
        script.AppendLine("xorriso -osirrox on -indev \"$SOURCE_ISO\" -extract / \"$ISO_DIR\"");
        script.AppendLine("SQUASHFS=$(find \"$ISO_DIR\" -type f -name filesystem.squashfs -print -quit)");
        script.AppendLine("if [ -z \"$SQUASHFS\" ]; then");
        script.AppendLine("    echo 'Unsupported ISO: no filesystem.squashfs live root filesystem was found.' >&2");
        script.AppendLine("    exit 2");
        script.AppendLine("fi");
        script.AppendLine("SQUASHFS_ISO_PATH=\"/${SQUASHFS#\"$ISO_DIR\"/}\"");
        script.AppendLine("unsquashfs -d \"$ROOTFS\" \"$SQUASHFS\"");
        script.AppendLine("if [ ! -f \"$ROOTFS/etc/os-release\" ]; then");
        script.AppendLine("    echo 'Unsupported live filesystem: /etc/os-release is missing.' >&2");
        script.AppendLine("    exit 2");
        script.AppendLine("fi");
        script.AppendLine("DISTRO_ID=$(sed -n 's/^ID=//p' \"$ROOTFS/etc/os-release\" | head -n 1 | tr -d '\"' | tr '[:upper:]' '[:lower:]')");
        script.AppendLine("case \"$DISTRO_ID\" in");
        script.AppendLine("    ubuntu|debian) ;;");
        script.AppendLine("    *) echo \"Unsupported source distribution '$DISTRO_ID'. Upload an Ubuntu or Debian live ISO.\" >&2; exit 2 ;;");
        script.AppendLine("esac");
        script.AppendLine("echo \"Detected source distribution: $DISTRO_ID\"");
        script.AppendLine("ROOTFS_BINARY=$(LC_ALL=C file -bL \"$ROOTFS/usr/bin/dpkg\")");
        script.AppendLine("case \"$ROOTFS_BINARY\" in");
        script.AppendLine("    *x86-64*) ;;");
        script.AppendLine("    *) echo 'Unsupported source architecture. Upload an x86_64 (amd64) live ISO.' >&2; exit 2 ;;");
        script.AppendLine("esac");
        script.AppendLine("echo '[STAGE 3/5] Enabling official repositories and installing desktop/tools...'");
        script.AppendLine("if [ \"$DISTRO_ID\" = ubuntu ]; then");
        script.AppendLine("    for source in \"$ROOTFS\"/etc/apt/sources.list \"$ROOTFS\"/etc/apt/sources.list.d/*.list; do");
        script.AppendLine("        [ -f \"$source\" ] || continue");
        script.AppendLine("        sed -i -E '/^[[:space:]]*deb(-src)?[[:space:]]/ { /universe/! s/( main)([[:space:]]|$)/\\1 restricted universe multiverse\\2/; }' \"$source\"");
        script.AppendLine("    done");
        script.AppendLine("    for source in \"$ROOTFS\"/etc/apt/sources.list.d/*.sources; do");
        script.AppendLine("        [ -f \"$source\" ] || continue");
        script.AppendLine("        sed -i '/^Components:/ { /universe/! s/$/ universe multiverse/; }' \"$source\"");
        script.AppendLine("    done");
        script.AppendLine("else");
        script.AppendLine("    for source in \"$ROOTFS\"/etc/apt/sources.list \"$ROOTFS\"/etc/apt/sources.list.d/*.list; do");
        script.AppendLine("        [ -f \"$source\" ] || continue");
        script.AppendLine("        sed -i -E '/^[[:space:]]*deb(-src)?[[:space:]]/ { /contrib/! s/( main)([[:space:]]|$)/\\1 contrib non-free non-free-firmware\\2/; }' \"$source\"");
        script.AppendLine("    done");
        script.AppendLine("    for source in \"$ROOTFS\"/etc/apt/sources.list.d/*.sources; do");
        script.AppendLine("        [ -f \"$source\" ] || continue");
        script.AppendLine("        sed -i '/^Components:/ { /contrib/! s/$/ contrib non-free non-free-firmware/; }' \"$source\"");
        script.AppendLine("    done");
        script.AppendLine("fi");
        if (recipe.Packages.Contains("google-chrome-stable", StringComparer.OrdinalIgnoreCase))
        {
            script.AppendLine("install -d -m 0755 \"$ROOTFS/etc/apt/keyrings\"");
            script.AppendLine("curl -fsSL https://dl.google.com/linux/linux_signing_key.pub -o \"$WORKDIR/google-linux-signing-key.pub\"");
            script.AppendLine("gpg --dearmor --batch --yes --output \"$ROOTFS/etc/apt/keyrings/google-chrome.gpg\" \"$WORKDIR/google-linux-signing-key.pub\"");
            script.AppendLine("chmod 0644 \"$ROOTFS/etc/apt/keyrings/google-chrome.gpg\"");
            script.AppendLine("printf '%s\\n' 'deb [arch=amd64 signed-by=/etc/apt/keyrings/google-chrome.gpg] https://dl.google.com/linux/chrome/deb/ stable main' > \"$ROOTFS/etc/apt/sources.list.d/google-chrome.list\"");
        }
        script.AppendLine("DESKTOP_ENVIRONMENT=\"" + recipe.DesktopEnvironment + "\"");
        script.AppendLine("DESKTOP_PACKAGES=''");
        script.AppendLine("DISPLAY_MANAGER=''");
        script.AppendLine("case \"$DISTRO_ID:$DESKTOP_ENVIRONMENT\" in");
        script.AppendLine("    ubuntu:xfce) DESKTOP_PACKAGES='xubuntu-desktop-minimal lightdm'; DISPLAY_MANAGER=lightdm.service ;;");
        script.AppendLine("    ubuntu:gnome) DESKTOP_PACKAGES='ubuntu-desktop-minimal'; DISPLAY_MANAGER=gdm3.service ;;");
        script.AppendLine("    ubuntu:kde) DESKTOP_PACKAGES='kubuntu-desktop'; DISPLAY_MANAGER=sddm.service ;;");
        script.AppendLine("    ubuntu:i3) DESKTOP_PACKAGES='i3 lightdm'; DISPLAY_MANAGER=lightdm.service ;;");
        script.AppendLine("    debian:xfce) DESKTOP_PACKAGES='xfce4 lightdm'; DISPLAY_MANAGER=lightdm.service ;;");
        script.AppendLine("    debian:gnome) DESKTOP_PACKAGES='gnome-core gdm3'; DISPLAY_MANAGER=gdm3.service ;;");
        script.AppendLine("    debian:kde) DESKTOP_PACKAGES='kde-plasma-desktop sddm'; DISPLAY_MANAGER=sddm.service ;;");
        script.AppendLine("    debian:i3) DESKTOP_PACKAGES='i3 lightdm'; DISPLAY_MANAGER=lightdm.service ;;");
        script.AppendLine("esac");
        script.AppendLine("PACKAGE_LIST=$(printf '%s\\n' \"$DESKTOP_PACKAGES " + packageList + "\" | xargs)");
        script.AppendLine("mkdir -p \"$ROOTFS/dev/pts\" \"$ROOTFS/proc\" \"$ROOTFS/sys\" \"$ROOTFS/run\"");
        script.AppendLine("RESOLV_LINK=''");
        script.AppendLine("if [ -L \"$ROOTFS/etc/resolv.conf\" ]; then");
        script.AppendLine("    RESOLV_LINK=$(readlink \"$ROOTFS/etc/resolv.conf\")");
        script.AppendLine("elif [ -f \"$ROOTFS/etc/resolv.conf\" ]; then");
        script.AppendLine("    cp \"$ROOTFS/etc/resolv.conf\" \"$WORKDIR/resolv.conf.original\"");
        script.AppendLine("fi");
        script.AppendLine("cp -L /etc/resolv.conf \"$ROOTFS/etc/resolv.conf\"");
        script.AppendLine("mount --bind /dev \"$ROOTFS/dev\"");
        script.AppendLine("mount --bind /dev/pts \"$ROOTFS/dev/pts\"");
        script.AppendLine("mount -t proc proc \"$ROOTFS/proc\"");
        script.AppendLine("mount --bind /sys \"$ROOTFS/sys\"");
        script.AppendLine("mount --bind /run \"$ROOTFS/run\"");
        script.AppendLine("export DEBIAN_FRONTEND=noninteractive");
        script.AppendLine("chroot \"$ROOTFS\" apt-get update");
        script.AppendLine("if [ -n \"$PACKAGE_LIST\" ]; then");
        script.AppendLine("    chroot \"$ROOTFS\" apt-get install -y --no-install-recommends $PACKAGE_LIST");
        script.AppendLine("fi");
        script.AppendLine("if [ -n \"$DISPLAY_MANAGER\" ]; then");
        script.AppendLine("    chroot \"$ROOTFS\" systemctl enable \"$DISPLAY_MANAGER\"");
        script.AppendLine("fi");
        script.AppendLine("if [ -f \"$ISO_DIR/casper/filesystem.manifest\" ]; then");
        script.AppendLine("    chroot \"$ROOTFS\" dpkg-query --show --showformat='${Package} ${Version}\\n' | sort > \"$ISO_DIR/casper/filesystem.manifest\"");
        script.AppendLine("fi");
        script.AppendLine("if [ -f \"$ISO_DIR/casper/filesystem.size\" ]; then");
        script.AppendLine("    du -sx --block-size=1 \"$ROOTFS\" | cut -f1 > \"$ISO_DIR/casper/filesystem.size\"");
        script.AppendLine("fi");
        script.AppendLine("if [ -n \"$RESOLV_LINK\" ]; then");
        script.AppendLine("    rm -f \"$ROOTFS/etc/resolv.conf\"");
        script.AppendLine("    ln -s \"$RESOLV_LINK\" \"$ROOTFS/etc/resolv.conf\"");
        script.AppendLine("elif [ -f \"$WORKDIR/resolv.conf.original\" ]; then");
        script.AppendLine("    cp \"$WORKDIR/resolv.conf.original\" \"$ROOTFS/etc/resolv.conf\"");
        script.AppendLine("else");
        script.AppendLine("    rm -f \"$ROOTFS/etc/resolv.conf\"");
        script.AppendLine("fi");
        script.AppendLine("cleanup");
        script.AppendLine("trap - EXIT");
        script.AppendLine("echo '[STAGE 4/5] Rebuilding the live filesystem image...'");
        script.AppendLine("mksquashfs \"$ROOTFS\" \"$SQUASHFS.repacked\" -noappend -comp xz");
        script.AppendLine("mv \"$SQUASHFS.repacked\" \"$SQUASHFS\"");
        script.AppendLine("echo '[STAGE 5/5] Rebuilding the bootable ISO and preserving its boot metadata...'");
        script.AppendLine("xorriso -indev \"$SOURCE_ISO\" -outdev \"$OUTPUT_ISO\" -boot_image any replay -map \"$SQUASHFS\" \"$SQUASHFS_ISO_PATH\" -commit -end");
        script.AppendLine("test -s \"$OUTPUT_ISO\"");
        script.AppendLine("echo \"Customized ISO created: $OUTPUT_ISO\"");
        script.AppendLine("ls -lh \"$OUTPUT_ISO\"");

        return script.ToString();
    }
}
