# AI-ISO-Creator 💿🤖

Nowoczesna aplikacja do personalizacji dostarczonego przez użytkownika Linux Live ISO. Gemini podpowiada pakiety i przykładowe komendy, a osobne moduły dystrybucji wykonują zweryfikowane zmiany w jednorazowym, izolowanym chroot.

---

## ✨ Kluczowe Funkcjonalności

1. **Generowanie Receptur przez Gemini AI**:
   - Gemini pełni rolę asystenta pakietów: proponuje nazwy pakietów do instalacji/usunięcia oraz przykładowe komendy do ręcznego użycia. Wybór środowiska graficznego i parametry ISO ustawia użytkownik.
   - Użytkownik dostarcza bazowy obraz Ubuntu, Debian, Zorin OS, Fedora, Arch Linux lub openSUSE Live ISO; dedykowany moduł dystrybucji konfiguruje repozytoria i natywny menedżer pakietów, instaluje XFCE, GNOME, KDE, i3 i wybrane aplikacje oraz zachowuje metadane rozruchowe obrazu.
   - Tryb **Refinement** (doprecyzowywanie receptury): możliwość konwersacyjnego dopracowania systemu z AI (np. *"Dodaj Wi-Fi i usuń edytor tekstu"*).
   - Tryb **Offline Fallback**: aplikacja posiada wbudowane algorytmy szablonów, działające nawet bez natychmiastowo podanego klucza API.

2. **Lekki i Nowoczesny Interfejs Graficzny (GUI)**:
   - Nowoczesny dashboard w ciemnym motywie (Dark Modern / Cyber-slate).
   - Wskaźniki stanu w czasie rzeczywistym: dostępność Gemini AI, Podman i wirtualizacji QEMU.
   - Interaktywny edytor receptury: wybór środowiska graficznego i zarządzanie pakietami.
   - Wbudowane gotowe presety promptów uruchamiane jednym kliknięciem.

3. **Wielozadaniowy Silnik Budowy ISO (Podman Rootless)**:
   - Każdy build działa w jednorazowym kontenerze Podmana; wewnątrz niego tworzony jest chroot z systemu plików przesłanego ISO.
   - Osobny moduł Ubuntu/Debian/Zorin, Fedora, Arch i openSUSE konfiguruje repozytoria oraz natywne komendy instalacji i usuwania pakietów.
   - Strumieniowanie logów konsoli budowania w czasie rzeczywistym przez **Server-Sent Events (SSE)**.
   - Pasek postępu z podziałem na etapy (narzędzia -> rozpakowanie źródła -> repozytoria i pakiety -> SquashFS -> ISO).
   - Odtworzenie informacji rozruchowych BIOS/UEFI z obrazu źródłowego.

**Obsługiwane źródła:** x86_64 Live ISO Ubuntu, Debian, Zorin OS, Fedora, Arch Linux i openSUSE zawierające `filesystem.squashfs`, `rootfs.sfs`, `airootfs.sfs` albo obsługiwany obraz `LiveOS/squashfs.img` z `rootfs.img`/`ext3fs.img`. Podczas wypakowywania pomijane są statyczne wpisy `/dev`, których rootless Podman nie może utworzyć; środowisko Live odtwarza je przy starcie. Inne układy ISO, obrazy instalacyjne/server, Alpine i inne architektury nie są obsługiwane. Nazwy pakietów są zależne od repozytoriów wybranej dystrybucji; popularne aliasy są mapowane przez moduły. Limit przesyłania wynosi 16 GiB. Wymagane jest połączenie sieciowe z repozytoriami podczas budowania.

**Uwaga dotycząca APT:** moduł Ubuntu/Debian/Zorin dopuszcza niezaufane repozytoria i pakiety APT, aby umożliwić budowanie obrazów Live pozbawionych `gpgv` lub korzystających z repozytoriów bez poprawnej sygnatury. Wyłącza to weryfikację autentyczności źródeł APT podczas kompilacji; używaj wyłącznie zaufanych sieci i repozytoriów.

Komendy zwracane przez Gemini są wyłącznie podpowiedziami i nie są wykonywane. Build uruchamia jedynie operacje pakietowe z modułu rozpoznanej dystrybucji, po walidacji nazw pakietów.

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

Rozwiązanie zawiera zestaw testów jednostkowych weryfikujących personalizację przesłanych obrazów, generowanie receptur, tryby awaryjne, sanityzację oraz dostępność narzędzi systemowych:

```bash
dotnet test
```

---

## 📁 Struktura Projektu

- **`src/AI-ISO-Creator.Core/`**:
  - `Models/`: Modele `IsoRecipe`, `BuildJob`, `BuildLogEntry`.
  - `Services/`: `GeminiAiService` (obsługa Gemini REST API i fallbacków), `ContainerBuilderService` (silnik Podmana i QEMU), `UploadedIsoBuildScript` oraz `UploadedIsoModules/` (osobne moduły APT, DNF, pacman i zypper).
  - `Configuration/`: Ustawienia modeli i kluczy Gemini.
- **`src/AI-ISO-Creator.Api/`**:
  - `Program.cs`: Endpointy Minimal API, obsługa SSE oraz serwowanie aplikacji.
  - `wwwroot/`: Nowoczesny frontend SPA (HTML5, `app.css`, `app.js`).
- **`tests/AI-ISO-Creator.Tests/`**:
  - Testy jednostkowe xUnit dla serwisów Core.
