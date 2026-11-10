# Zmiany w Turniejo

Każda zmiana, którą zauważy użytkownik, dostaje tu jedną linijkę w sekcji „Nieopublikowane” (w tym samym PR). Przy wydaniu sekcja zmienia się w numer wersji. Zasady numeracji: [docs/wersjonowanie.md](docs/wersjonowanie.md).

Kategorie: **Nowe** (funkcje), **Zmienione** (inaczej niż wcześniej), **Poprawione** (błędy), **Usunięte**.

## [Nieopublikowane]

### Zmienione

- Mecze w portalu kibica filtrujesz jednym paskiem: drużyna, kategoria, grupa, boisko i dzień. Widać, ile meczów pasuje, a filtry da się usunąć jednym kliknięciem i przesłać linkiem.

### Nowe

- Po wydaniu nowej wersji aplikacja pokazuje okno „Co nowego”, a pełna lista zmian jest pod linkiem w stopce.
- Przy dodawaniu kategorii wybierasz liczbę grup, a Grupa A, B… powstają od razu.
- Jasny motyw: domyślnie jak w telefonie lub komputerze, a ikonka w nagłówku przełącza między jasnym i ciemnym na każdej stronie, także przy logowaniu. Wybór zapisuje się na koncie.
- Kreator turnieju z grupami ma krok „Tabela”, w którym ustawisz kolejność przy równej liczbie punktów.
- Drabinka pucharu mieści dowolną liczbę drużyn, nie tylko 16. Przy większej liczbie pojawia się 1/16 finału, 1/32 finału i dalej.
- Na liście drużyn turnieju przycisk „Akceptuj wszystkie” przyjmuje od razu wszystkie oczekujące zgłoszenia, w granicach limitu drużyn.
- Przy zmianie terminu meczu kolejne mecze na tym samym boisku przesuwają się o tyle samo (opcja domyślnie zaznaczona), więc opóźnienie nie wymaga poprawiania każdego meczu osobno. Jeśli przez to drużyna albo sędzia nie zdążyłby na mecz na innym boisku, aplikacja pyta, czy przesunąć też tamte mecze.
- Strona główna pokazuje turnieje publiczne: trwające (z liczbą meczów na żywo), nadchodzące i zakończone. Można szukać po nazwie i filtrować po statusie, terminie, organizatorze i kategorii, a link z filtrami da się komuś wysłać. Z portalu turnieju wrócisz do listy jednym kliknięciem.
- W ustawieniach portalu przełącznik „Pokazuj na liście turniejów” ukrywa turniej ze strony głównej, a portal dalej działa przez link.

### Zmienione

- Grupy w karcie kategorii są czytelną listą z liczbą drużyn i przyciskami edycji i usuwania przy każdej grupie.
- Stopka pokazuje sam numer wersji, np. „wersja 1.0.0”. Szczegóły widać po najechaniu na numer.
- Turniej ma od razu domyślną kolejność reguł przy równej liczbie punktów (mecze bezpośrednie, bilans, gole, zwycięstwa). Organizator może ją zmienić.

### Poprawione

- Terminarz turnieju z kilkoma kategoriami powstaje dla wszystkich kategorii naraz i żadna nie zostaje bez meczów. Ta sama drużyna grająca w dwóch kategoriach nigdy nie ma dwóch meczów jednocześnie, a drużyny dostają jak najdłuższe przerwy bez wydłużania dnia. W formacie grupy + puchar mecze pucharu mają godziny od razu, a po wyczyszczeniu terminarza drabinka zostaje i planuje się ponownie.
- Mecz, którego nikt nie zakończył, nie trwa już w nieskończoność. Gdy przez 4 godziny nikt nic w nim nie zrobi, kończy się sam z aktualnym wynikiem. Remis w pucharze zostaje wstrzymany z napisem „Czeka na rozstrzygnięcie” zamiast zegara, a organizator dostaje powiadomienie, żeby rozstrzygnąć go karnymi.
- Grupę, z której wycofano wszystkie drużyny, da się już usunąć.
- Tabela grupy pokazuje drużyny od razu, jeszcze przed pierwszym rozegranym meczem.
- Tabela w panelu organizatora sama pokazuje wynik zakończonego meczu, bez odświeżania strony.
- Po przypisaniu drużyny do grupy, dodaniu albo wycofaniu drużyny tabela zmienia się od razu, bez odświeżania strony.
- Lista drużyn turnieju nie zmienia kolejności po akceptacji, więc przy drużynach o tej samej nazwie widać, którą faktycznie zaakceptowano.

## [1.0.0] - 2026-11-09

Pierwsze oficjalne wydanie Turniejo: cały turniej, od zapisów drużyn po wyniki na żywo, w jednym miejscu.

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
