# Integracja z Gemini AI

## Przegląd
Moduł integracji z Gemini AI (`GeminiAiService`) odpowiada za przekształcanie intencji użytkownika wyrażonej w naturalnym języku w precyzyjną, maszynowo czytelną recepturę budowy systemu Linux (ISO Recipe).

## Specyfikacja Modelu
- **Rekomendowane modele**: `gemini-flash-latest` (domyślny, szybki), `gemini-flash-lite-latest`, `gemini-3-flash-preview` lub `gemini-3.1-pro-preview`.
- **Wielomodelowy Fallback**: W przypadku napotkania limitu lub wycofania starszego modelu, system automatycznie ponawia próbę z aktywnym modelem `gemini-flash-latest`.
- **Format komunikacji**: REST API Google Generative Language (`v1beta/models/...:generateContent`) z wymuszeniem ustrukturyzowanego JSON (`responseMimeType: "application/json"`).

## System Prompt
Model otrzymuje system prompt definiujący schemat wyjściowy JSON zawierający:
- `distribution`: bazowy system (np. alpine, fedora, debian)
- `packages`: lista pakietów do zainstalowania
- `services`: usługi systemowe do włączenia (np. ssh, NetworkManager)
- `customCommands`: dodatkowe skrypty powłoki wykonywane podczas chroot
- `kernelParameters`: parametry jądra Linuxa
