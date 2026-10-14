# Gemini AI Integration

## Overview
The `GeminiAiService` module translates high-level natural language user requirements into a strictly typed, machine-executable Linux build specification (`IsoRecipe`).

## Supported Models
- **`gemini-2.5-flash`** (Default): Fast, responsive, and highly accurate for package selection and OS configurations.
- **`gemini-2.5-pro`**: Recommended for complex, multi-package desktop setups or specialized kernel parameters.
- **`gemini-1.5-flash` / `gemini-1.5-pro`**: Fully supported as fallback options.

## API Communication
The service communicates with Google Generative Language REST endpoint:
```
POST https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey}
```
With `generationConfig`:
```json
{
  "temperature": 0.2,
  "responseMimeType": "application/json"
}
```

## System Prompt Contract
The model is instructed to strictly adhere to the following JSON schema:
```json
{
  "distribution": "alpine" | "debian",
  "release": "3.20" | "bookworm",
  "arch": "x86_64",
  "isoLabel": "SYSTEM_LABEL",
  "hostname": "hostname",
  "rootPassword": "root",
  "defaultUsername": "user",
  "defaultUserPassword": "live",
  "enableSudo": true,
  "desktopEnvironment": "none" | "xfce" | "i3",
  "packages": ["package1", "package2"],
  "servicesToEnable": ["sshd", "networking"],
  "customCommands": ["echo setup > /etc/note.txt"],
  "kernelParameters": "quiet console=tty0",
  "summary": "Detailed technical rationale of chosen packages and distribution.",
  "targetUseCase": "Short summary of the system purpose"
}
```

## Conversational Recipe Refinement
The user can modify an already generated recipe by providing natural language follow-up instructions (e.g., *"Add Docker, switch hostname to dev-box, and remove htop"*). The service transmits the existing recipe JSON alongside the user request, allowing Gemini to incrementally adjust packages and configurations.

## Resilient Offline Fallback
If no API key is configured or network is unreachable, `GeminiAiService` contains an intelligent heuristic engine that inspects keywords (e.g. *rescue*, *developer*, *network*, *desktop*) to produce a fully viable, bootable Linux recipe offline.
