# Zmiany w Turniejo

Każda zmiana, którą zauważy użytkownik, dostaje tu jedną linijkę w sekcji „Nieopublikowane” (w tym samym PR). Przy wydaniu sekcja zmienia się w numer wersji. Zasady numeracji: [docs/wersjonowanie.md](docs/wersjonowanie.md).

Kategorie: **Nowe** (funkcje), **Zmienione** (inaczej niż wcześniej), **Poprawione** (błędy), **Usunięte**.

## [Nieopublikowane]

### Nowe

- Po wydaniu nowej wersji aplikacja pokazuje okno „Co nowego”, a pełna lista zmian jest pod linkiem w stopce.
- Przy dodawaniu kategorii wybierasz liczbę grup, a Grupa A, B… powstają od razu.

### Zmienione

- Grupy w karcie kategorii są czytelną listą z liczbą drużyn i przyciskami edycji i usuwania przy każdej grupie.

### Poprawione

- Grupę, z której wycofano wszystkie drużyny, da się już usunąć.

### Poprawione

- Tabela grupy pokazuje drużyny od razu, jeszcze przed pierwszym rozegranym meczem.
- Tabela w panelu organizatora sama pokazuje wynik zakończonego meczu, bez odświeżania strony.

### Zmienione

- Stopka pokazuje sam numer wersji, np. „wersja 1.0.0”. Szczegóły widać po najechaniu na numer.

## [1.0.0] - 2026-11-09

Pierwsza wersja z numerem. Ten wpis streszcza, co aplikacja umie w tym momencie.

### Nowe

- Konta przez Keycloak: rejestracja organizatora albo uczestnika, logowanie, reset hasła mailem, zmiana hasła, eksport i usunięcie konta (RODO).
- Organizacja i baza drużyn z kadrą zawodników i herbami.
- Turnieje w trzech formatach: grupy, puchar, grupy + puchar. Kreator konfiguracji: format, zasady gry, portal kibica, kategorie, boiska, drużyny.
- Zapisy drużyn z limitem i statusem opłaty, składy turniejowe z kontrolą wieku i numerów, import z kadry.
- Generator terminarza z planem dni, limitem meczów na dzień i kontrolą kolizji boisk, drużyn i sędziów.
- Drabinka pucharowa z wolnymi losami, meczem o 3. miejsce i awansem z grup.
- Mecz na żywo dla sędziego: zegar z pauzą, gole, kartki, asysty, MVP, koniec meczu z dogrywką, karnymi albo walkowerem, poprawki w oknie edycji. Pula meczów dla sędziów.
- Tabele grup z kryteriami rozstrzygania remisów, statystyki strzelców, asyst i MVP.
- Panele trenera i rodzica, powiadomienia w aplikacji i przypomnienia o meczach.
- Publiczny portal kibica z wynikami na żywo, wyborem sekcji przez organizatora i skracaniem nazwisk zawodników.
- Numer wersji w stopce aplikacji i portalu kibica.
