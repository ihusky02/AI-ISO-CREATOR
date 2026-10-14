# Architektura Systemu AI-ISO-Creator

## Przegląd
Aplikacja została zaprojektowana w oparciu o czystą architekturę modułową (Modular Monolith) w języku C# (.NET 8/10), łączącą backend API, silnik przetwarzania AI oraz lekki interfejs graficzny typu Web-Desktop.

## Struktura Modułów
- **AI-ISO-Creator.Core**: Biblioteka rdzenna zawierająca modele danych (prompty, receptury, statusy), usługi komunikacji z Gemini AI oraz orkiestrator budowania ISO.
- **AI-ISO-Creator.Api**: Serwer ASP.NET Core Minimal API udostępniający endpointy REST dla interfejsu użytkownika oraz przesyłania strumieniowego logów przez WebSockets / Server-Sent Events.
- **AI-ISO-Creator.Frontend**: Statyczny interfejs użytkownika (HTML5, Bootstrap 5, Modern CSS, JavaScript) osadzony i serwowany przez API, uruchamiany w dedykowanym okienku przeglądarki (`google-chrome --app`).
- **AI-ISO-Creator.Tests**: Testy jednostkowe i integracyjne weryfikujące poprawność parsowania receptur AI oraz logikę budowania.

## Przepływ Danych (Data Flow)
1. Użytkownik wpisuje prompt w GUI.
2. API przekazuje prompt do `GeminiAiService`.
3. Gemini zwraca ustrukturyzowaną recepturę JSON (pakiety, skrypty konfiguracyjne).
4. Receptura trafia do edytora w GUI do wglądu/modyfikacji.
5. Po zatwierdzeniu, `ContainerBuilderService` uruchamia rootless kontener Podman, który wykonuje bootstrap systemu, instalację pakietów, kompresję SquashFS oraz generowanie obrazu ISO przez `genisoimage`/`xorriso`.
6. Logi budowania są strumieniowane w czasie rzeczywistym do konsoli w GUI.
