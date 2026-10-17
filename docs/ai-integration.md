# Integracja z Gemini AI

## Zakres odpowiedzialności

`GeminiAiService` pomaga przygotować plan zmian pakietów dla dostarczonego przez użytkownika Linux Live ISO. Odpowiedź obejmuje:

- `packages` — pakiety do zainstalowania;
- `packagesToRemove` — pakiety do usunięcia;
- `suggestedCommands` — tekstowe przykłady komend do ręcznego wykorzystania przez użytkownika.

Gemini nie wybiera ani nie zmienia dystrybucji, wydania, architektury, środowiska graficznego, etykiety ISO, kont użytkowników ani konfiguracji rozruchu. Te decyzje należą do użytkownika lub do modułu wybranej na podstawie rzeczywistego obrazu dystrybucji. Odpowiedź modelu nie jest skryptem budowania.

## Przepływ i walidacja

Żądania są wysyłane do Google Generative Language API z oczekiwanym formatem JSON. Odpowiedź jest parsowana i ograniczana do pól planu pakietów. Nazwy pakietów muszą pasować do dozwolonego formatu tokenu, listy mają limit 100 elementów i nie mogą jednocześnie zawierać tego samego pakietu do instalacji i usunięcia. Sugestie komend mają limit liczby i długości; odrzucane są m.in. operatory powłoki oraz znaki przekierowań.

Walidacja ogranicza format odpowiedzi, ale nie potwierdza, że pakiet istnieje w repozytoriach konkretnej wersji dystrybucji. Użytkownik powinien sprawdzić plan przed budowaniem.

Komendy z `suggestedCommands` są wyświetlane jako tekst i nie są przekazywane do `chroot`, powłoki ani kontenera. Podczas budowania uruchamiane są wyłącznie operacje pakietowe zdefiniowane w module wykrytej dystrybucji. Nazwy pakietów przechodzą dodatkową walidację po stronie budowniczego.

## Tryb bez klucza API

Jeśli klucz Gemini nie jest skonfigurowany, aplikacja generuje plan z lokalnych, deterministycznych szablonów. Fallback nie wyszukuje informacji w internecie. Podczas lokalnego doprecyzowania może rozpoznać proste prośby o dodanie lub usunięcie pakietów; nie zapewnia pełnego rozumienia języka modelu.

## Konfiguracja

Klucz API i wybrany model konfiguruje się w ustawieniach aplikacji. Lista modeli zależy od konfiguracji i dostępności po stronie Google; w razie niepowodzenia usługi aplikacja próbuje skonfigurowane modele zapasowe. Nie należy umieszczać klucza API w recepturze ani w repozytorium.
