# AI-ISO-Creator.Core

Projekt biblioteki klas stanowiący rdzeń logiczny aplikacji **AI-ISO-Creator**.

## Zawartość modułu
- **Models/**: Definicje klas danych dla receptur ISO, zapytań promptowych, konfiguracji AI oraz zadań budowania.
- **Services/**: Implementacje usług biznesowych:
  - `GeminiAiService`: Komunikacja z API Google Gemini, generowanie i uaktualnianie receptur ISO.
  - `PodmanBuildEngine`: Kontenerowy silnik budujący obrazy ISO z wykorzystaniem Podmana (rootless).
  - `BuildJobManager`: Zarządzanie zadaniami budowania, kolejkowanie i buforowanie logów w czasie rzeczywistym.
  - `SystemInspectorService`: Wykrywanie narzędzi w systemie hosta (Podman, genisoimage, QEMU).
- **Templates/**: Szablony skryptów budowania i presety dla popularnych dystrybucji (Alpine, Debian).
