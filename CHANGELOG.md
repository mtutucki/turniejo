# Zmiany w Turniejo

Każda zmiana, którą zauważy użytkownik, dostaje tu jedną linijkę w sekcji „Nieopublikowane” (w tym samym PR). Przy wydaniu sekcja zmienia się w numer wersji. Zasady numeracji: [docs/wersjonowanie.md](docs/wersjonowanie.md).

Kategorie: **Nowe** (funkcje), **Zmienione** (inaczej niż wcześniej), **Poprawione** (błędy), **Usunięte**.

## [Nieopublikowane]

## [1.1.0] - 2026-11-10

### Zmienione

- Wycofanie drużyny przy gotowym terminarzu najpierw pyta o potwierdzenie i mówi, że jej nierozegrane mecze zostaną odwołane (w trwającym pucharze rywal dostaje walkower).
- Na liście drużyn turnieju (także w kreatorze) każda grupa ma własną ramkę z nazwą i liczbą drużyn, a drużyny bez grupy są wyróżnione na górze.
- Mecze w portalu kibica filtrujesz jednym paskiem: drużyna, kategoria, grupa, boisko i dzień. Widać, ile meczów pasuje, a filtry da się usunąć jednym kliknięciem i przesłać linkiem.
- Terminarz w panelu da się przefiltrować po drużynie. Drużyna grająca w kilku kategoriach jest na liście raz i pokazuje mecze ze wszystkich. Grupy o tej samej nazwie mają przy sobie kategorię (np. „U10 · Grupa B”), a pod filtrami widać, ile meczów pasuje i które filtry są włączone.
- Podtytuł strony głównej nie sugeruje już, że bez logowania można coś więcej niż oglądać wyniki.

### Nowe

- Nowy format „Dwie fazy grupowe”: po grupach drużyny grają w drugich grupach, np. miejsca 1-2 ze wszystkich grup w Grupie I, a miejsca 3-4 w Grupie II. Podział ustawiasz zakresami (np. Grupa I: miejsca 1-2), także zanim drużyny trafią do grup, a bez ustawień terminarz podzieli miejsca po równo. Pojedyncze miejsca też da się przestawić. Mecze drugiej fazy dostają godziny od razu, a drużyny trafiają do nich same po ostatnim meczu swojej grupy.
- Drużyny w bazie grupujesz etykietami, np. „Rocznik 2015” albo „Klub Orlik”, a drużyna może mieć kilka. Etykiety dodajesz w zakładce „Etykiety”, dla wielu drużyn naraz albo w oknie edycji drużyny. Baza drużyn dzieli się na zwijane sekcje po etykietach. Przy zgłaszaniu do turnieju drużyny też są w sekcjach, całą etykietę zaznaczasz jednym polem, a filtr pozwala wybrać kilka etykiet naraz.
- Drużyna w bazie drużyn ma statystyki ze wszystkich turniejów: bilans, bramki, procent zwycięstw i formę, listę turniejów z wynikiem (np. „Zwycięzca”, „Odpadła w półfinale”, „2. miejsce w Grupie A”), najlepszych zawodników (gole, asysty, MVP, kartki) i bilans z każdym rywalem. Trener ma te statystyki w zakładce „Wszystkie turnieje” swojej drużyny.
- Każda drużyna ma swoją stronę: miejsce w grupie, bilans, forma z ostatnich meczów, wszystkie mecze z wynikami, droga w pucharze i skład z golami i asystami. Wchodzisz na nią, klikając drużynę w tabeli, drabince, na liście drużyn albo na stronie meczu, w portalu kibica i w panelu.
- Turnieje łatwiej znaleźć w Google: strona główna i portale kibica mają opisy, sitemapę i dane o meczach. Link do turnieju albo meczu wysłany na Messengerze, WhatsAppie czy Facebooku pokazuje obrazek z nazwą turnieju, a przy meczu z drużynami i aktualnym wynikiem. Turniej ukryty z listy turniejów nie trafia do Google.
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

- Logo na liście turniejów publicznych nie przenosi już zalogowanego do panelu, tylko zostawia go na liście. Do panelu prowadzi przycisk „Mój panel”.
- Po wygaśnięciu sesji w portalu kibica zostajesz w portalu jako niezalogowany, zamiast trafiać na stronę logowania.
- Po wygenerowaniu terminarza Terminarz nie odsyła już z powrotem do generatora (także później, po wyczyszczeniu terminarza).
- Po wygenerowaniu terminarza nie da się już przenieść drużyny do innej grupy ani przyjąć nowej. Wcześniej mecze zostawały ze starym podziałem i tabele grup były błędne. Żeby to zmienić, najpierw wyczyść terminarz.
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
