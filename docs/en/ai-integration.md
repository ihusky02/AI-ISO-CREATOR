# Gemini AI Integration

## Overview
The `GeminiAiService` module translates high-level natural language user requirements into a strictly typed, machine-executable Linux build specification (`IsoRecipe`).

## Supported Models
- **`gemini-flash-latest`** (Default): High performance, ultra-fast latency, and highly accurate for package selection and OS configurations.
- **`gemini-flash-lite-latest`**: Extremely lightweight, highly responsive, and reliable without rate limiting spikes.
- **`gemini-3-flash-preview`**: Next-generation Gemini 3 preview model.
- **`gemini-3.1-pro-preview`**: Deep reasoning model for advanced custom distribution recipes.

## Resilient Multi-Model Fallback
If the user selects a model that encounters a temporary quota limit (429), temporary spike (503), or is deprecated (404), `GeminiAiService` automatically performs a seamless fallback to `gemini-flash-latest` or `gemini-flash-lite-latest`, ensuring the build recipe generation is never interrupted.
