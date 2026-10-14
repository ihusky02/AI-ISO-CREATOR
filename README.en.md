# AI-ISO-Creator 💿🤖

A modern, lightweight graphical application integrated with **Google Gemini AI** that creates tailored, bootable Linux ISO images (x86_64 Live System) using natural language prompts.

[Polska wersja językowa dostępna w pliku README.md](./README.md)

---

## ✨ Key Features

1. **AI-Powered Linux Recipe Generation**:
   - Translates natural language requests into structured system recipes (`IsoRecipe`).
   - Automatically determines base distribution (Alpine / Debian), essential toolsets, desktop environments (XFCE, i3, or CLI), and configuration scripts.
   - **Recipe Refinement**: Conversational follow-ups to tweak packages or settings (e.g., *"Add Docker and remove text editor"*).
   - **Offline Fallback Engine**: Built-in template matching when an API key is not immediately configured.

2. **Lightweight Modern Graphical Interface (GUI)**:
   - Sleek dark modern dashboard with real-time status indicators (Gemini AI, Podman, and QEMU).
   - Interactive package tag manager and configuration editor.
   - Built-in prompt presets for one-click kickstarts (Dev workstation, Rescue disk, Network auditor, Lightweight XFCE desktop, Web kiosk).

3. **Robust Rootless ISO Build Engine**:
   - Safe compilation using rootless Podman containers without host system pollution.
   - Live console output streamed to the frontend via **Server-Sent Events (SSE)**.
   - Hybrid ISO creation compatible with both legacy **BIOS MBR** and modern **UEFI**.

4. **One-Click Testing and Verification**:
   - Automatic SHA256 checksum calculation.
   - Direct browser download of generated ISOs.
   - **One-click QEMU test**: Test the newly generated ISO in a virtual machine instantly.
   - Build history with options to redownload or clean up past images.

---

## 🚀 Quick Start

### Prerequisites
- **.NET 8.0 SDK** (or newer)
- **Podman** (rootless mode)
- Optional: **QEMU** (`qemu-system-x86_64`) for instant virtual machine testing.

### Launching the Application

Run the startup script:
```bash
./start.sh
```

The application will build, start the web server at `http://localhost:5000`, and open the UI in your default web browser.

Alternatively, launch using the .NET CLI:
```bash
dotnet run --project src/AI-ISO-Creator.Api --urls "http://localhost:5000"
```

---

## ⚙️ Gemini AI Configuration

1. Click **"⚙️ AI Settings"** in the top-right header of the application.
2. Enter your API key from [Google AI Studio](https://aistudio.google.com/).
3. Choose your preferred model (e.g. `gemini-2.5-flash` or `gemini-2.5-pro`).
4. Click **"Test Connection"**, then **"Save Settings"**. The configuration is saved locally in `user_settings.json`.

---

## 🧪 Running Unit Tests

Run the xUnit test suite covering Gemini recipe generation, fallback logic, sanitation, and tool detection:
```bash
dotnet test
```

---

## 📁 Project Architecture

- **`src/AI-ISO-Creator.Core/`**:
  - `Models/`: `IsoRecipe`, `BuildJob`, `BuildLogEntry`.
  - `Services/`: `GeminiAiService` (Google Gemini REST API & fallback logic), `ContainerBuilderService` (Podman orchestrator, SquashFS, xorriso & QEMU runner).
  - `Configuration/`: Model options and endpoints.
- **`src/AI-ISO-Creator.Api/`**:
  - `Program.cs`: Minimal API endpoints, SSE log streaming, static asset serving.
  - `wwwroot/`: Modern SPA web interface (HTML5, `app.css`, `app.js`).
- **`tests/AI-ISO-Creator.Tests/`**:
  - Comprehensive xUnit unit tests.
