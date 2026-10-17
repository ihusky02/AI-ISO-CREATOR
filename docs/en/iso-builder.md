# ISO Customization Engine and Distribution Modules

## Build model

`ContainerBuilderService` starts a Podman container and a dedicated workspace for each build. The container provides ISO extraction and repackaging tools; it does not bootstrap a new target root filesystem. The chroot is created from the filesystem SquashFS contained in the user's Live ISO.

## Stages

1. Install tools for ISO, SquashFS, filesystem, and key handling.
2. Extract the source image with `xorriso` and unpack a supported SquashFS image. Static `/dev` entries are skipped; the directories and devices needed by the chroot are provided through bind mounts.
3. Read `/etc/os-release`, verify x86_64 architecture, and select a distribution module.
4. Configure repositories; refresh package metadata; remove requested packages; install the selected desktop and user packages inside the chroot.
5. Repack the filesystem as SquashFS and create an ISO with `xorriso`, replaying boot metadata from the source image.

For Ubuntu/Debian images with a `casper` manifest, the package manifest is also refreshed when present. The chroot temporarily uses the build container's DNS configuration while downloading packages; the original `resolv.conf` is restored before repacking.

## Supported source images

The input must be an x86_64 Live ISO with a recognized `/etc/os-release` and one of these supported filesystem images:

- `filesystem.squashfs`
- `rootfs.sfs`
- `airootfs.sfs`
- `LiveOS/squashfs.img` (with `LiveOS/rootfs.img` or `LiveOS/ext3fs.img` when that layout is used)

Supported modules cover Ubuntu, Debian, Zorin OS, Fedora, Arch Linux, and openSUSE. Standard installers, server images without a recognized Live layout, Alpine, unknown distribution IDs, and other architectures are unsupported. Package availability depends on the repositories and release in the source image.

The UI upload limit is 16 GiB. Builds require network access to working repositories; the process is not offline.

## Repository and package modules

| Family | Repository preparation | Update / install / remove |
|---|---|---|
| Ubuntu/Zorin | Enables APT `restricted`, `universe`, and `multiverse` components in supported source formats. | `apt-get update`, `apt-get install`, `apt-get remove --purge` |
| Debian | Enables `contrib`, `non-free`, and, for selected releases, `non-free-firmware`. | `apt-get update`, `apt-get install`, `apt-get remove --purge` |
| Fedora | Uses repositories configured in the source image. | DNF |
| Arch | Uncomments the configured `multilib` repository when the entries are present. | pacman |
| openSUSE | Enables existing zypper repositories. | zypper |

Modules map selected package-name aliases across distributions. This does not guarantee that every package is available or that every Live image supports desktop installation.

The user chooses one of the supported desktop options: keep the source desktop unchanged (`none`), XFCE, GNOME, KDE Plasma, or i3. The module installs the associated packages and enables its display manager with systemd.

## Security

- Container isolation reduces the build's impact on the host, but it does not make an untrusted ISO, package, or repository safe. Package maintainer scripts execute as root inside the chroot.
- The APT module intentionally allows insecure repositories and packages (`AllowInsecureRepositories`, `AllowDowngradeToInsecureRepositories`, and `--allow-unauthenticated`) to support images without `gpgv` and repositories with invalid signatures. This disables APT authenticity verification during the build. Use only trusted images and sources.
- Gemini command suggestions are not executed. The builder validates package names and uses operations defined by the distribution module.

## Troubleshooting

Build logs identify the current stage and command errors. DNS errors mean the container or chroot cannot reach repositories. “Unable to locate package” can indicate missing package indexes, an unsupported package name, or source repositories that do not match the ISO release. Distribution-detection errors or a missing SquashFS image indicate an unsupported input layout. Test the resulting ISO in a virtual machine before using it on physical hardware.
