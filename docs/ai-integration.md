# Integracja z Gemini AI

## Przegląd
Moduł integracji z Gemini AI (`GeminiAiService`) odpowiada za przekształcanie intencji użytkownika wyrażonej w naturalnym języku w precyzyjną, maszynowo czytelną recepturę budowy systemu Linux (ISO Recipe).

## Specyfikacja Modelu
- **Rekomendowane modele**: `gemini-2.5-flash` (domyślny, szybki) lub `gemini-2.5-pro` (dla skomplikowanych konfiguracji kastomowych).
- **Format komunikacji**: REST API Google Generative Language (`v1beta/models/...:generateContent`) z wymuszeniem strukturalnego JSON (JSON Schema / Structured Outputs).

## System Prompt
Model otrzymuje system prompt definiujący schemat wyjściowy JSON zawierający:
- `distribution`: bazowy system (np. alpine, debian, ubuntu)
- `packages`: lista pakietów do zainstalowania
- `services`: usługi systemowe do włączenia (np. ssh)
- `customCommands`: dodatkowe skrypty powłoki wykonywane podczas chroot
- `kernelParameters`: parametry jądra Linuxa
