# AI-ISO-Creator 💿🤖

Nowoczesna, lekka i graficzna aplikacja zintegrowana z **Google Gemini AI**, umożliwiająca tworzenie w pełni funkcjonalnych, bootowalnych obrazów ISO systemu Linux (x86_64 Live System) za pomocą promptów w języku naturalnym.

---

## ✨ Kluczowe Funkcjonalności

1. **Generowanie Receptur przez Gemini AI**:
   - Inteligentne tłumaczenie promptu (np. *"Dystrybucja do diagnostyki dysków i ratowania danych"*) na ustrukturyzowaną recepturę systemu (`IsoRecipe`).
   - Dobór optymalnej bazy (Alpine Linux / Debian), dedykowanych pakietów, środowiska pulpitu (XFCE, i3 lub CLI) oraz skryptów konfiguracji.
   - Tryb **Refinement** (doprecyzowywanie receptury): możliwość konwersacyjnego dopracowania systemu z AI (np. *"Dodaj Wi-Fi i usuń edytor tekstu"*).
   - Tryb **Offline Fallback**: aplikacja posiada wbudowane algorytmy szablonów, działające nawet bez natychmiastowo podanego klucza API.

2. **Lekki i Nowoczesny Interfejs Graficzny (GUI)**:
   - Nowoczesny dashboard w ciemnym motywie (Dark Modern / Cyber-slate).
   - Wskaźniki stanu w czasie rzeczywistym: dostępność Gemini AI, Podman i wirtualizacji QEMU.
   - Interaktywny edytor receptury: zarządzanie pakietami za pomocą tagów, konfiguracja użytkowników, haseł i jądra.
   - Wbudowane gotowe presety promptów uruchamiane jednym kliknięciem.

3. **Wielozadaniowy Silnik Budowy ISO (Podman Rootless)**:
   - Bezpieczna i izolowana kompilacja systemu w kontenerach bez uprawnień administratora (`podman`).
   - Strumieniowanie logów konsoli budowania w czasie rzeczywistym przez **Server-Sent Events (SSE)**.
   - Pasek postępu z podziałem na etapy (Narzędzia -> Pakiety -> RootFS -> SquashFS -> Bootloader -> ISO).
   - Generowanie hybrydowego obrazu ISO wspierającego zarówno starszy **BIOS MBR**, jak i nowoczesne **UEFI**.

4. **Weryfikacja i Testowanie Jednym Kliknięciem**:
   - Automatyczne wyliczanie sumy kontrolnej SHA256 oraz rozmiaru pliku.
   - Bezpośrednie pobieranie pliku `.iso` przez przeglądarkę.
   - **Błyskawiczny test w QEMU**: przycisk *"Uruchom w QEMU"* natychmiast odpala nowo wygenerowany system w maszynie wirtualnej bez dotykania pendrive'a!
   - Historia zbudowanych obrazów z możliwością ponownego pobrania lub usunięcia.

---

## 🚀 Szybki Start

### Wymagania wstępne
- **.NET 8.0 SDK** (lub nowszy)
- **Podman** (wersja rootless)
- Opcjonalnie: **QEMU** (`qemu-system-x86_64`) dla funkcji podglądu w maszynie wirtualnej.

### Uruchomienie aplikacji

Wystarczy wykonać skrypt:
```bash
./start.sh
```

Aplikacja skompiluje się, uruchomi serwer HTTP pod adresem `http://localhost:5000` i automatycznie otworzy interfejs w Twojej domyślnej przeglądarce.

Możesz także uruchomić ją bezpośrednio przez .NET CLI:
```bash
dotnet run --project src/AI-ISO-Creator.Api --urls "http://localhost:5000"
```

---

## ⚙️ Konfiguracja Gemini AI

1. Kliknij przycisk **"⚙️ Ustawienia AI"** w prawym górnym rogu interfejsu aplikacji.
2. Wklej swój klucz API z [Google AI Studio](https://aistudio.google.com/).
3. Wybierz preferowany model (np. `gemini-2.5-flash` lub `gemini-2.5-pro`).
4. Kliknij *"Przetestuj Połączenie"*, a następnie *"Zapisz Ustawienia"*. Klucz zostanie trwale zachowany w lokalnym pliku `user_settings.json`.

---

## 🧪 Uruchamianie Testów

Rozwiązanie zawiera zestaw testów jednostkowych weryfikujących generowanie receptur, tryby awaryjne, sanityzację oraz dostępność narzędzi systemowych:

```bash
dotnet test
```

---

## 📁 Struktura Projektu

- **`src/AI-ISO-Creator.Core/`**:
  - `Models/`: Modele `IsoRecipe`, `BuildJob`, `BuildLogEntry`.
  - `Services/`: `GeminiAiService` (obsługa Gemini REST API i fallbacków), `ContainerBuilderService` (silnik Podmana, SquashFS, xorriso i integracja QEMU).
  - `Configuration/`: Ustawienia modeli i kluczy Gemini.
- **`src/AI-ISO-Creator.Api/`**:
  - `Program.cs`: Endpointy Minimal API, obsługa SSE oraz serwowanie aplikacji.
  - `wwwroot/`: Nowoczesny frontend SPA (HTML5, `app.css`, `app.js`).
- **`tests/AI-ISO-Creator.Tests/`**:
  - Testy jednostkowe xUnit dla serwisów Core.
