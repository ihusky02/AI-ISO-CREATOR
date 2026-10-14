# ISO Build Engine and Podman Containerization

## Overview
The `ContainerBuilderService` compiles bootable Linux ISO images in an isolated, secure, rootless environment using Podman and native Linux filesystem tools (`mksquashfs`, `xorriso`, `isolinux`, `grub`).

## Build Stages

```
[STAGE 1/5] Container Initialization & Toolchain Setup
    ├── Install xorriso, syslinux, squashfs-tools, grub-efi, dosfstools, shadow
[STAGE 2/5] Target RootFS Bootstrap
    ├── Download base system (Alpine Base or Debian rootfs)
    └── Install requested packages and Linux LTS kernel
[STAGE 3/5] Target System Customization
    ├── Configure Hostname, Root password, Default user
    ├── Grant Sudo permissions (wheel group)
    ├── Enable OpenRC / systemd services
    └── Execute custom shell commands
[STAGE 4/5] SquashFS Compression & Bootloader Preparation
    ├── Compress rootfs into rootfs.squashfs using XZ compression
    ├── Deploy ISOLINUX configuration for legacy BIOS MBR
    └── Deploy GRUB EFI configuration for modern UEFI systems
[STAGE 5/5] Hybrid Bootable ISO Generation
    ├── Pack ISO image using xorriso with hybrid MBR/UEFI boot support
    └── Calculate SHA256 checksum and verify file integrity
```

## Security & Isolation
- **No Host Root Privileges Needed**: Runs entirely via user-mode rootless Podman (`--privileged` or user namespace flags mapped inside the container only).
- **Workspace Isolation**: Each build job is assigned a dedicated workspace directory (`build_artifacts/{jobId}`) that contains generated scripts, build logs, and final output artifacts.
- **Resource Management**: Jobs can be cancelled at any time; cancellation sends a termination signal to the container and cleans up active resources.

## Virtual Machine Testing (QEMU)
Once built, the engine integrates directly with QEMU:
```bash
qemu-system-x86_64 -m 2048 -cdrom "/path/to/image.iso" -boot d -vga std
```
KVM hardware acceleration is automatically detected and leveraged (`-enable-kvm`) if `/dev/kvm` is accessible.
