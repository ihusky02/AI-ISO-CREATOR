using System.Text;
using AI_ISO_Creator.Core.Models;
using AI_ISO_Creator.Core.Services.UploadedIsoModules;

namespace AI_ISO_Creator.Core.Services;

internal static class UploadedIsoBuildScript
{
    private static readonly IUploadedIsoDistroModule[] DistroModules =
    [
        new DebianUbuntuModule(),
        new FedoraModule(),
        new ArchModule(),
        new OpenSuseModule()
    ];

    public static string Generate(IsoRecipe recipe, string jobId)
    {
        var outputFileName = $"{recipe.IsoLabel.ToLowerInvariant()}-{jobId}.iso";
        var script = new StringBuilder();
        script.AppendLine("#!/bin/bash");
        script.AppendLine("set -Eeuo pipefail");
        script.AppendLine("WORKDIR=/workspace");
        script.AppendLine("SOURCE_ISO=\"$WORKDIR/source.iso\"");
        script.AppendLine("ISO_DIR=\"$WORKDIR/iso\"");
        script.AppendLine("SQUASHFS_CONTENTS=\"$WORKDIR/squashfs-contents\"");
        script.AppendLine("ROOTFS=\"$WORKDIR/rootfs\"");
        script.AppendLine("OUT_DIR=\"$WORKDIR/output\"");
        script.AppendLine("OUTPUT_ISO=\"$OUT_DIR/" + outputFileName + "\"");
        script.AppendLine("ROOTFS_IMAGE_MOUNTED=0");
        script.AppendLine("RESOLV_CONF_STATE=unchanged");
        script.AppendLine("RESOLV_LINK=''");
        script.AppendLine("cleanup() {");
        script.AppendLine("    if [ \"$RESOLV_CONF_STATE\" = symlink ]; then");
        script.AppendLine("        rm -f \"$ROOTFS/etc/resolv.conf\"");
        script.AppendLine("        ln -s \"$RESOLV_LINK\" \"$ROOTFS/etc/resolv.conf\"");
        script.AppendLine("        RESOLV_CONF_STATE=unchanged");
        script.AppendLine("    elif [ \"$RESOLV_CONF_STATE\" = file ]; then");
        script.AppendLine("        rm -f \"$ROOTFS/etc/resolv.conf\"");
        script.AppendLine("        cp \"$WORKDIR/resolv.conf.original\" \"$ROOTFS/etc/resolv.conf\"");
        script.AppendLine("        RESOLV_CONF_STATE=unchanged");
        script.AppendLine("    elif [ \"$RESOLV_CONF_STATE\" = missing ]; then");
        script.AppendLine("        rm -f \"$ROOTFS/etc/resolv.conf\"");
        script.AppendLine("        RESOLV_CONF_STATE=unchanged");
        script.AppendLine("    fi");
        script.AppendLine("    for mountpoint in run sys dev/pts dev proc; do");
        script.AppendLine(
            "        mountpoint -q \"$ROOTFS/$mountpoint\" && umount -lf \"$ROOTFS/$mountpoint\" 2>/dev/null || true");
        script.AppendLine("    done");
        script.AppendLine("    if [ \"$ROOTFS_IMAGE_MOUNTED\" -eq 1 ]; then");
        script.AppendLine("        umount -lf \"$ROOTFS\" 2>/dev/null || true");
        script.AppendLine("        ROOTFS_IMAGE_MOUNTED=0");
        script.AppendLine("    fi");
        script.AppendLine("}");
        script.AppendLine("trap cleanup EXIT");
        script.AppendLine("mkdir -p \"$ISO_DIR\" \"$SQUASHFS_CONTENTS\" \"$ROOTFS\" \"$OUT_DIR\"");
        script.AppendLine("echo '[STAGE 1/5] Installing ISO repackaging tools...'");
        script.AppendLine(
            "dnf install -y xorriso squashfs-tools util-linux coreutils findutils curl gnupg2 file e2fsprogs");
        script.AppendLine("echo '[STAGE 2/5] Extracting the uploaded live ISO...'");
        script.AppendLine("xorriso -osirrox on -indev \"$SOURCE_ISO\" -extract / \"$ISO_DIR\"");
        script.AppendLine(
            "SQUASHFS=$(find \"$ISO_DIR\" -type f \\( -name filesystem.squashfs -o -name rootfs.sfs -o -name airootfs.sfs -o -path '*/LiveOS/squashfs.img' \\) -print -quit)");
        script.AppendLine("if [ -z \"$SQUASHFS\" ]; then");
        script.AppendLine("    echo 'Unsupported ISO: no supported live SquashFS image was found.' >&2");
        script.AppendLine("    exit 2");
        script.AppendLine("fi");
        script.AppendLine("SQUASHFS_ISO_PATH=\"/${SQUASHFS#\"$ISO_DIR\"/}\"");
        script.AppendLine(
            "echo 'Skipping static /dev entries during extraction; the live system populates /dev at boot.'");
        script.AppendLine("unsquashfs -excludes -d \"$SQUASHFS_CONTENTS\" \"$SQUASHFS\" 'dev/*'");
        script.AppendLine("mkdir -p \"$SQUASHFS_CONTENTS/dev\"");
        script.AppendLine(
            "ROOTFS_IMAGE=$(find \"$SQUASHFS_CONTENTS/LiveOS\" -maxdepth 1 -type f \\( -name rootfs.img -o -name ext3fs.img \\) -print -quit 2>/dev/null || true)");
        script.AppendLine("if [ -n \"$ROOTFS_IMAGE\" ]; then");
        script.AppendLine("    mount -o loop \"$ROOTFS_IMAGE\" \"$ROOTFS\"");
        script.AppendLine("    ROOTFS_IMAGE_MOUNTED=1");
        script.AppendLine("else");
        script.AppendLine("    ROOTFS=\"$SQUASHFS_CONTENTS\"");
        script.AppendLine("fi");
        script.AppendLine("if [ ! -f \"$ROOTFS/etc/os-release\" ]; then");
        script.AppendLine("    echo 'Unsupported live filesystem: /etc/os-release is missing.' >&2");
        script.AppendLine("    exit 2");
        script.AppendLine("fi");
        script.AppendLine(
            "DISTRO_ID=$(sed -n 's/^ID=//p' \"$ROOTFS/etc/os-release\" | head -n 1 | tr -d '\"' | tr '[:upper:]' '[:lower:]')");
        script.AppendLine("ROOTFS_BINARY=$(LC_ALL=C file -bL \"$ROOTFS/bin/sh\")");
        script.AppendLine("case \"$ROOTFS_BINARY\" in");
        script.AppendLine("    *x86-64*) ;;");
        script.AppendLine(
            "    *) echo 'Unsupported source architecture. Upload an x86_64 (amd64) live ISO.' >&2; exit 2 ;;");
        script.AppendLine("esac");
        script.AppendLine("echo \"Detected source distribution: $DISTRO_ID\"");
        script.AppendLine("echo '[STAGE 3/5] Enabling repositories and installing desktop/tools...'");
        script.AppendLine("DESKTOP_ENVIRONMENT=\"" + recipe.DesktopEnvironment + "\"");
        script.AppendLine("DESKTOP_PACKAGES=''");
        script.AppendLine("DISPLAY_MANAGER=''");
        script.AppendLine("PACKAGE_MANAGER_UPDATE=''");
        script.AppendLine("PACKAGE_MANAGER_INSTALL=''");
        script.AppendLine("PACKAGE_MANAGER_REMOVE=''");
        script.AppendLine("SERVICE_MANAGER=''");
        script.AppendLine("case \"$DISTRO_ID\" in");
        foreach (var module in DistroModules)
        {
            module.AppendSelectionCase(script);
        }

        script.AppendLine(
            "    *) echo \"Unsupported source distribution '$DISTRO_ID'. No customization module is available.\" >&2; exit 2 ;;");
        script.AppendLine("esac");
        if (recipe.Packages.Contains("google-chrome-stable", StringComparer.OrdinalIgnoreCase))
        {
            script.AppendLine("case \"$DISTRO_ID\" in");
            script.AppendLine("    ubuntu|debian|zorin)");
            script.AppendLine("        install -d -m 0755 \"$ROOTFS/etc/apt/keyrings\"");
            script.AppendLine(
                "        curl -fsSL https://dl.google.com/linux/linux_signing_key.pub -o \"$WORKDIR/google-linux-signing-key.pub\"");
            script.AppendLine(
                "        gpg --dearmor --batch --yes --output \"$ROOTFS/etc/apt/keyrings/google-chrome.gpg\" \"$WORKDIR/google-linux-signing-key.pub\"");
            script.AppendLine("        chmod 0644 \"$ROOTFS/etc/apt/keyrings/google-chrome.gpg\"");
            script.AppendLine(
                "        printf '%s\\n' 'deb [arch=amd64 signed-by=/etc/apt/keyrings/google-chrome.gpg] https://dl.google.com/linux/chrome/deb/ stable main' > \"$ROOTFS/etc/apt/sources.list.d/google-chrome.list\"");
            script.AppendLine("        ;;");
            script.AppendLine(
                "    *) echo 'google-chrome-stable is supported only by the Debian/Ubuntu module.' >&2; exit 2 ;;");
            script.AppendLine("esac");
        }

        AppendUserPackages(script, recipe);
        AppendPackagesToRemove(script, recipe);
        script.AppendLine("PACKAGE_LIST=$(printf '%s\\n' \"$DESKTOP_PACKAGES $USER_PACKAGES\" | xargs)");
        script.AppendLine("REMOVE_PACKAGE_LIST=$(printf '%s\\n' \"$USER_PACKAGES_TO_REMOVE\" | xargs)");
        script.AppendLine("mkdir -p \"$ROOTFS/dev/pts\" \"$ROOTFS/proc\" \"$ROOTFS/sys\" \"$ROOTFS/run\"");
        script.AppendLine("if [ -L \"$ROOTFS/etc/resolv.conf\" ]; then");
        script.AppendLine("    RESOLV_LINK=$(readlink \"$ROOTFS/etc/resolv.conf\")");
        script.AppendLine("    RESOLV_CONF_STATE=symlink");
        script.AppendLine("elif [ -f \"$ROOTFS/etc/resolv.conf\" ]; then");
        script.AppendLine("    cp \"$ROOTFS/etc/resolv.conf\" \"$WORKDIR/resolv.conf.original\"");
        script.AppendLine("    RESOLV_CONF_STATE=file");
        script.AppendLine("else");
        script.AppendLine("    RESOLV_CONF_STATE=missing");
        script.AppendLine("fi");
        script.AppendLine("rm -f \"$ROOTFS/etc/resolv.conf\"");
        script.AppendLine("cp -L /etc/resolv.conf \"$ROOTFS/etc/resolv.conf\"");
        script.AppendLine("echo 'Using the build container DNS configuration in the target system while installing packages.'");
        script.AppendLine("mount --bind /dev \"$ROOTFS/dev\"");
        script.AppendLine("mount --bind /dev/pts \"$ROOTFS/dev/pts\"");
        script.AppendLine("mount -t proc proc \"$ROOTFS/proc\"");
        script.AppendLine("mount --bind /sys \"$ROOTFS/sys\"");
        script.AppendLine("mount --bind /run \"$ROOTFS/run\"");
        script.AppendLine("chroot \"$ROOTFS\" /bin/sh -c \"$PACKAGE_MANAGER_UPDATE\"");
        script.AppendLine("if [ -n \"$REMOVE_PACKAGE_LIST\" ]; then");
        script.AppendLine("    echo \"Removing requested packages: $REMOVE_PACKAGE_LIST\"");
        script.AppendLine("    chroot \"$ROOTFS\" /bin/sh -c \"$PACKAGE_MANAGER_REMOVE $REMOVE_PACKAGE_LIST\"");
        script.AppendLine("fi");
        script.AppendLine("if [ -n \"$PACKAGE_LIST\" ]; then");
        script.AppendLine("    echo \"Installing requested packages: $PACKAGE_LIST\"");
        script.AppendLine("    chroot \"$ROOTFS\" /bin/sh -c \"$PACKAGE_MANAGER_INSTALL $PACKAGE_LIST\"");
        script.AppendLine("fi");
        script.AppendLine("if [ -n \"$DISPLAY_MANAGER\" ]; then");
        script.AppendLine("    case \"$SERVICE_MANAGER\" in");
        script.AppendLine("        systemd) chroot \"$ROOTFS\" systemctl enable \"$DISPLAY_MANAGER\" ;;");
        script.AppendLine("        *) echo \"Unsupported service manager '$SERVICE_MANAGER'.\" >&2; exit 2 ;;");
        script.AppendLine("    esac");
        script.AppendLine("fi");
        script.AppendLine(
            "if [ -f \"$ISO_DIR/casper/filesystem.manifest\" ] && { [ \"$DISTRO_ID\" = ubuntu ] || [ \"$DISTRO_ID\" = debian ] || [ \"$DISTRO_ID\" = zorin ]; }; then");
        script.AppendLine(
            "    chroot \"$ROOTFS\" dpkg-query --show --showformat='${Package} ${Version}\\n' | sort > \"$ISO_DIR/casper/filesystem.manifest\"");
        script.AppendLine("fi");
        script.AppendLine("if [ -f \"$ISO_DIR/casper/filesystem.size\" ]; then");
        script.AppendLine("    du -sx --block-size=1 \"$ROOTFS\" | cut -f1 > \"$ISO_DIR/casper/filesystem.size\"");
        script.AppendLine("fi");
        script.AppendLine("cleanup");
        script.AppendLine("trap - EXIT");
        script.AppendLine("echo '[STAGE 4/5] Rebuilding the live filesystem image...'");
        script.AppendLine("mksquashfs \"$SQUASHFS_CONTENTS\" \"$SQUASHFS.repacked\" -noappend -comp xz");
        script.AppendLine("mv \"$SQUASHFS.repacked\" \"$SQUASHFS\"");
        script.AppendLine("echo '[STAGE 5/5] Rebuilding the bootable ISO and preserving its boot metadata...'");
        script.AppendLine(
            "xorriso -indev \"$SOURCE_ISO\" -outdev \"$OUTPUT_ISO\" -boot_image any replay -map \"$SQUASHFS\" \"$SQUASHFS_ISO_PATH\" -commit -end");
        script.AppendLine("test -s \"$OUTPUT_ISO\"");
        script.AppendLine("echo \"Customized ISO created: $OUTPUT_ISO\"");
        script.AppendLine("ls -lh \"$OUTPUT_ISO\"");

        return script.ToString();
    }

    private static void AppendUserPackages(StringBuilder script, IsoRecipe recipe)
    {
        script.AppendLine("USER_PACKAGES=''");
        script.AppendLine("case \"$DISTRO_ID\" in");
        foreach (var module in DistroModules)
        {
            var packages = recipe.Packages
                .Select(module.MapPackage)
                .Distinct(StringComparer.OrdinalIgnoreCase);
            script.AppendLine(
                $"    {string.Join("|", module.DistributionIds)}) USER_PACKAGES='{string.Join(" ", packages)}' ;;");
        }

        script.AppendLine("esac");
    }

    private static void AppendPackagesToRemove(StringBuilder script, IsoRecipe recipe)
    {
        script.AppendLine("USER_PACKAGES_TO_REMOVE=''");
        script.AppendLine("case \"$DISTRO_ID\" in");
        foreach (var module in DistroModules)
        {
            var packages = recipe.PackagesToRemove
                .Select(module.MapPackage)
                .Distinct(StringComparer.OrdinalIgnoreCase);
            script.AppendLine(
                $"    {string.Join("|", module.DistributionIds)}) USER_PACKAGES_TO_REMOVE='{string.Join(" ", packages)}' ;;");
        }

        script.AppendLine("esac");
    }
}