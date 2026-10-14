#!/usr/bin/env bash
# AI-ISO-Creator - Application Quickstart Script
set -e

DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" >/dev/null 2>&1 && pwd)"
cd "$DIR"

echo "=========================================================="
echo "      🚀 Launching AI-ISO-Creator (Gemini AI + Podman)"
echo "=========================================================="

# Check for .NET SDK
if ! command -v dotnet &> /dev/null; then
    echo "❌ Error: .NET SDK was not found in PATH."
    exit 1
fi

# Check for Podman container runtime
if ! command -v podman &> /dev/null; then
    echo "⚠️ Warning: Podman was not found. ISO compilation requires Podman."
else
    echo "✓ Detected Podman: $(podman --version)"
fi

# Check for QEMU virtualization
if command -v qemu-system-x86_64 &> /dev/null; then
    echo "✓ Detected QEMU: virtualization is ready for instant ISO testing"
fi

PORT=5000
URL="http://localhost:$PORT"

echo "Building and launching web server at $URL..."

# Open browser in the background after a brief delay
(
    sleep 2
    if command -v xdg-open &> /dev/null; then
        xdg-open "$URL" > /dev/null 2>&1 || true
    fi
) &

dotnet run --project src/AI-ISO-Creator.Api --urls "$URL"
