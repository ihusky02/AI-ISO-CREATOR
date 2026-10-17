# AI-ISO-Creator Architecture

## Overview

The .NET 8 application consists of an API, core domain logic, distribution-specific modules for customizing uploaded Live ISO images, and a static web interface. Podman isolates each build job from the host; system changes are applied in a chroot created from the user's uploaded filesystem image.

## Projects

- **AI-ISO-Creator.Core** — domain models (`IsoRecipe`, `BuildJob`), `GeminiAiService`, `ContainerBuilderService`, build-script generation, and distribution modules.
- **AI-ISO-Creator.Api** — ASP.NET Core Minimal API for ISO uploads, build status and logs, and serving the frontend from `wwwroot`.
- **Frontend (`wwwroot`)** — HTML, CSS, and JavaScript for selecting the ISO, reviewing the recipe, choosing a desktop, and viewing build logs.
- **AI-ISO-Creator.Tests** — tests for recipe handling, validation, and build-script generation.

## Data flow

1. The user selects a supported Linux Live ISO and describes the packages they need.
2. `GeminiAiService` returns package additions, removals, and optional textual command suggestions. If no API key is configured, deterministic local fallback templates are used.
3. The user reviews and edits the plan, chooses a desktop, and sets the ISO label.
4. `ContainerBuilderService` starts a Podman container and assigns a dedicated workspace to the job.
5. The build script extracts the source ISO and its SquashFS filesystem, detects the distribution from `/etc/os-release`, and selects its module. The module enables or configures repositories, defines native package-manager commands, and maps the selected desktop.
6. Requested removals and installations run inside the chroot. Gemini does not supply or execute these commands; its textual suggestions are informational only.
7. The modified filesystem is repacked as SquashFS, and `xorriso` creates an ISO while replaying the source boot metadata.
8. The API streams logs and progress to the frontend, where the completed image can be downloaded.

## Distribution modules

Modules in `AI-ISO-Creator.Core/Services/UploadedIsoModules/` support these families:

| Module | Distribution IDs | Package manager |
|---|---|---|
| Debian/Ubuntu | `ubuntu`, `debian`, `zorin` | APT |
| Fedora | `fedora` | DNF |
| Arch | `arch`, `archlinux` | pacman |
| openSUSE | `opensuse`, `opensuse-leap`, `opensuse-tumbleweed` | zypper |

Each module defines repository configuration, update/install/remove commands, package-name mappings, and supported desktops. Selection is based on the detected source system ID, not on Gemini's response.

## Scope and assumptions

This is not a clean root filesystem bootstrapper or a distribution installer: the uploaded ISO is the base. The image must be an x86_64 Live ISO with a supported SquashFS layout and a distribution recognized by a module. See [the build engine documentation](./iso-builder.md) for format and compatibility details.
