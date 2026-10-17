# Architektura systemu AI-ISO-Creator

## Przegląd

Aplikacja w .NET 8 składa się z API, logiki domenowej, modułów personalizacji przesłanych obrazów Live ISO i statycznego interfejsu webowego. Podman izoluje zadanie budowania od hosta; właściwe zmiany systemu są wykonywane w chroot utworzonym z systemu plików obrazu użytkownika.

## Projekty

- **AI-ISO-Creator.Core** — modele (`IsoRecipe`, `BuildJob`), `GeminiAiService`, `ContainerBuilderService`, generator skryptu oraz moduły dystrybucji.
- **AI-ISO-Creator.Api** — ASP.NET Core Minimal API, obsługa przesyłania ISO, statusów i logów budowania oraz serwowanie interfejsu z `wwwroot`.
- **Frontend (`wwwroot`)** — HTML, CSS i JavaScript: wybór pliku ISO, edycja receptury, wybór pulpitu i prezentacja logów.
- **AI-ISO-Creator.Tests** — testy logiki receptur, walidacji i generowania skryptów.

## Przepływ danych

1. Użytkownik wybiera obsługiwane Linux Live ISO oraz opisuje potrzebne pakiety.
2. `GeminiAiService` zwraca listy pakietów do instalacji i usunięcia oraz opcjonalne, tekstowe sugestie komend. Bez skonfigurowanego klucza API używany jest lokalny fallback.
3. Użytkownik sprawdza i edytuje plan, wybiera pulpit i etykietę ISO.
4. `ContainerBuilderService` uruchamia kontener Podman i umieszcza źródłowy obraz w osobnym katalogu zadania.
5. Skrypt wyodrębnia pliki ISO oraz SquashFS, rozpoznaje dystrybucję z `/etc/os-release` i wybiera jej moduł. Moduł włącza lub konfiguruje repozytoria, ustawia natywne komendy menedżera pakietów i mapuje wybrany pulpit.
6. Pakiety są usuwane, a następnie instalowane w chrootie. Gemini nie dostarcza ani nie uruchamia tych komend; tekstowe sugestie są wyłącznie informacyjne.
7. Zmodyfikowany system plików jest ponownie pakowany do SquashFS, a ISO tworzone jest z zachowaniem metadanych rozruchowych obrazu źródłowego.
8. Logi i postęp są udostępniane przez API, a gotowy obraz można pobrać.

## Moduły dystrybucji

Moduły w `AI-ISO-Creator.Core/Services/UploadedIsoModules/` obsługują rodziny:

| Moduł | Identyfikatory dystrybucji | Menedżer |
|---|---|---|
| Debian/Ubuntu | `ubuntu`, `debian`, `zorin` | APT |
| Fedora | `fedora` | DNF |
| Arch | `arch`, `archlinux` | pacman |
| openSUSE | `opensuse`, `opensuse-leap`, `opensuse-tumbleweed` | zypper |

Każdy moduł określa konfigurację repozytoriów, komendy aktualizacji/instalacji/usuwania, mapowania nazw pakietów i dostępne pulpity. Wybór odbywa się na podstawie wykrytego identyfikatora systemu źródłowego, a nie deklaracji Gemini.

## Granice i założenia

To nie jest generator czystego rootfs ani instalator dystrybucji: podstawą jest ISO przesłane przez użytkownika. Obraz musi być x86_64 Live ISO z obsługiwanym układem SquashFS i dystrybucją rozpoznawaną przez moduły. Szczegóły formatów i ograniczeń znajdują się w [opisie silnika budowania](./iso-builder.md).
