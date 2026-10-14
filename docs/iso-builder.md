# Silnik Budowy ISO i Konteneryzacja Podman

## Przegląd
Silnik budowania obrazów (`ContainerBuilderService`) realizuje proces kompilacji systemu bez konieczności posiadania uprawnień administratora (rootless), wykorzystując kontener Podman oraz narzędzia generowania ISO.

## Fazy Procesu Budowy
1. **Inicjalizacja kontenera roboczego**: Uruchomienie kontenera Podman na bazie zdefiniowanej w recepturze (np. Alpine lub Debian).
2. **Instalacja pakietów**: Wykonanie menedżera pakietów (`apk` lub `apt`) wewnątrz kontenera zgodnie z listą z receptury AI.
3. **Konfiguracja środowiska**: Wykonanie poleceń customowych, konfiguracja użytkowników, włączenie usług systemowych.
4. **Tworzenie struktury Live ISO**: Przygotowanie katalogów `isolinux` / `grub`, skopiowanie jądra (`vmlinuz`) oraz initrd.
5. **Generowanie obrazu ISO**: Użycie `genisoimage` lub `xorriso` do spakowania struktury w bootowalny plik `.iso`.
