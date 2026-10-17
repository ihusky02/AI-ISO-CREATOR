# Silnik personalizacji ISO i moduły dystrybucji

## Model budowania

`ContainerBuilderService` uruchamia dla każdego zadania kontener Podman i dedykowany katalog roboczy. Kontener zawiera narzędzia do odczytu i ponownego pakowania ISO; system docelowy nie jest bootstrappowany od zera. Chroot powstaje z systemu plików SquashFS dostarczonego przez użytkownika Live ISO.

## Etapy

1. Instalacja narzędzi do obsługi ISO, SquashFS, systemów plików i kluczy.
2. Wyodrębnienie zawartości obrazu źródłowego przez `xorriso` i rozpakowanie obsługiwanego obrazu SquashFS. Statyczne wpisy `/dev` są pomijane; katalogi i urządzenia potrzebne chrootowi są udostępniane przez montowania bind.
3. Odczyt `/etc/os-release`, weryfikacja architektury x86_64 i wybór modułu dystrybucji.
4. Konfiguracja repozytoriów; aktualizacja indeksów; usunięcie wskazanych pakietów; instalacja wybranego pulpitu i pakietów użytkownika w chroot.
5. Ponowne spakowanie systemu do SquashFS i utworzenie ISO przez `xorriso` z odtworzeniem metadanych rozruchowych źródła.

W przypadku obrazu Ubuntu/Debian z manifestem `casper` aktualizowany jest również manifest pakietów, jeśli plik istnieje. DNS z kontenera jest tymczasowo wykorzystywany przez chroot podczas pobierania pakietów, a oryginalny `resolv.conf` jest odtwarzany przed repakowaniem.

## Obsługiwane źródła

Wymagany jest obraz x86_64 Live ISO z rozpoznanym `/etc/os-release` i jednym z obsługiwanych obrazów systemu plików:

- `filesystem.squashfs`
- `rootfs.sfs`
- `airootfs.sfs`
- `LiveOS/squashfs.img` (z `LiveOS/rootfs.img` lub `LiveOS/ext3fs.img`, jeśli system plików używa tego układu)

Obsługiwane moduły to Ubuntu, Debian, Zorin OS, Fedora, Arch Linux i openSUSE. Zwykłe instalatory, obrazy serwerowe bez rozpoznanego układu Live, Alpine, nieznane identyfikatory i inne architektury nie są obsługiwane. Dostępność pakietu zależy od źródeł i wydania zawartego w obrazie.

W interfejsie limit rozmiaru przesyłanego ISO wynosi 16 GiB. Budowanie wymaga dostępu do sieci i działających repozytoriów; nie jest to proces offline.

## Moduły repozytoriów i pakietów

| Rodzina | Przygotowanie źródeł | Aktualizacja / instalacja / usuwanie |
|---|---|---|
| Ubuntu/Zorin | Włącza komponenty APT `restricted`, `universe` i `multiverse` w obsługiwanych formatach źródeł. | `apt-get update`, `apt-get install`, `apt-get remove --purge` |
| Debian | Włącza `contrib`, `non-free` oraz — dla wskazanych wydań — `non-free-firmware`. | `apt-get update`, `apt-get install`, `apt-get remove --purge` |
| Fedora | Korzysta z repozytoriów skonfigurowanych w obrazie źródłowym. | DNF |
| Arch | Odkomentowuje skonfigurowane repozytorium `multilib`, jeśli wpisy są dostępne. | pacman |
| openSUSE | Włącza istniejące repozytoria zypper. | zypper |

Moduły mapują wybrane aliasy nazw pakietów między dystrybucjami. Nie gwarantuje to dostępności każdego pakietu ani kompatybilności dowolnego obrazu Live z instalacją pulpitu.

Pulpit wybiera użytkownik z dostępnych opcji: brak zmian (`none`), XFCE, GNOME, KDE Plasma lub i3. Moduł instaluje przypisane pakiety pulpitu i włącza odpowiadający mu menedżer logowania przez systemd.

## Bezpieczeństwo

- Izolacja kontenera ogranicza wpływ budowania na system hosta, ale nie czyni niezaufanego ISO, pakietu ani repozytorium bezpiecznym. Skrypty maintainerów pakietów są wykonywane jako root wewnątrz chroot.
- Moduł APT celowo dopuszcza niezaufane repozytoria i pakiety (`AllowInsecureRepositories`, `AllowDowngradeToInsecureRepositories`, `--allow-unauthenticated`), aby obsłużyć obrazy bez `gpgv` lub repozytoria bez poprawnych podpisów. Oznacza to brak weryfikacji autentyczności pakietów APT w czasie budowania. Używaj wyłącznie zaufanych źródeł i obrazów.
- Sugestie komend Gemini nie są wykonywane. Budowniczy waliduje nazwy pakietów i korzysta z operacji zdefiniowanych przez moduł dystrybucji.

## Diagnostyka

Log zadania wskazuje etap i błędy poleceń. Błędy DNS oznaczają, że kontener lub chroot nie może dotrzeć do repozytoriów. Błędy „unable to locate package” mogą wynikać z braku indeksów, nieobsługiwanej nazwy pakietu albo źródeł niepasujących do wydania w ISO. Błąd rozpoznania dystrybucji lub brak SquashFS oznacza nieobsługiwany układ wejściowy. Przed użyciem wyniku na sprzęcie sprawdź bootowalność zmodyfikowanego ISO w maszynie wirtualnej.
