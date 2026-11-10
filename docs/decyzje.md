# Decyzje

Nowa decyzja: dopisz wiersz z datą. Zmiana decyzji: popraw wiersz i dopisz datę zmiany, nie usuwaj historii z opisu.

## Architektura

| Temat | Decyzja | Dlaczego |
|---|---|---|
| Architektura | Modularny monolit: osobne projekty na moduł, granice pilnowane przez kompilator i testy architektury | jeden deploy na start, droga do wydzielenia usług bez przepisywania domeny |
| Logowanie | Keycloak + JWT, bez ASP.NET Identity; `User.Id` = `sub` z Keycloaka | hasła, tokeny, 2FA i logowanie przez Google po stronie Keycloaka |
| Baza | PostgreSQL, osobny schemat i migracje na moduł, bez kluczy obcych między modułami | izolacja modułów |
| Identyfikatory | Guid na wszystkich encjach, generowany w domenie | nie zdradza kolejności, nie wymaga bazy |
| Komunikacja modułów | interfejsy w SharedKernel (synchronicznie) + outbox ze zdarzeniami (asynchronicznie) | spójność transakcji bez rozproszonych transakcji |
| Usuwanie | twarde usuwanie albo status; „miękkie” tylko dla zdarzeń meczu (`MatchEvent`) | historia wyniku musi zostać |
| Enumy w bazie | jako `int` | mniejsze indeksy, zmiana nazwy bez migracji danych |
| Wynik meczu | liczony ze zdarzeń meczu, kolumny wyniku to tylko cache | cofnięcie gola samo poprawia wynik |
| Tabela i drabinka | projekcje przeliczane z meczów | zawsze da się odtworzyć od zera |
| Zegar meczu | `StartedAt`, `PausedAt`, `TotalPausedSeconds` na meczu | bez osobnej tabeli zegarów |
| Okno poprawek sędziego | `EditLockedAt` zapisywane przy końcu meczu | organizator może je zmienić, łatwe sprawdzanie |
| Mecz o 3. miejsce | `LoserGoesToNodeId` w węźle drabinki; ustawienie turnieju to tylko przełącznik przy tworzeniu | awans przegranych w samej strukturze drabinki |
| Drabinka a terminarz | drabinka tworzona osobno, przed terminarzem w każdej kategorii (także grupy + puchar); mecze kolejnych rund powstają, gdy znane są obie drużyny, w terminie zaplanowanym przez generator na węźle drabinki (2026-11-10) | mecz wymaga obu drużyn; plan dnia widać od razu |
| Maile | wysyła API przez SMTP (od 2026-10-12), reset hasła własnym linkiem jednorazowym | pełna kontrola treści, bez maili z Keycloaka |
| Powiadomienia | tylko w aplikacji; push na później | prostota V1 |
| Współbieżny zapis | dane turnieju: wersja (`UpdatedAt`); zasady gry i portal: wczytane wartości; konflikt = 409 i prośba o odświeżenie (2026-11-09) | dwóch organizatorów nie nadpisuje się po cichu |
| Okno „Co nowego” | tylko po wydaniu (X.Y.Z), jedno okno ze wszystkimi pominiętymi wersjami, tylko „Nowe” i „Zmienione”, ostatnia obejrzana wersja na koncie; nowe konto bez okna, istniejące konta startują od 1.0.0 (2026-11-09) | nie zasypujemy wracających wieloma oknami, okno nie wraca na innym urządzeniu |
| Przebieg wydań | funkcje i poprawki zbierane na `release/X.Y.Z` (lokalne merge'e bez PR), do `master` jeden PR na wydanie; pilna poprawka jako osobne mini-wydanie od `master` (2026-11-10) | pełna kontrola, PR i deploy raz na kilka zmian; jeden autor, więc PR na każdą zmianę nic nie daje |
| Motyw jasny/ciemny | domyślnie jak w systemie; ikonka na wierzchu każdej strony: panel, portal, mecz na żywo, logowanie i rejestracja, pomoc (nie w ustawieniach); zalogowany ma wybór na koncie i po zalogowaniu wygrywa on z wyborem z przeglądarki, kibic bez konta tylko w przeglądarce; zielony nagłówek marki taki sam w obu motywach (2026-11-10) | sędzia w słońcu przełącza jednym dotknięciem, wybór idzie za kontem między urządzeniami |

## Produkt

Ustalenia z przeglądu QA (2026-11-06 i 2026-11-08), wszystkie wdrożone i pokryte testami (ID w nawiasie).

**Role i dostęp**
- Sędzia prowadzi tylko mecz, do którego jest przypisany; mecz bez sędziego może wziąć z puli. Cudzy mecz prowadzi organizator (KON-020).
- Kody dostępu: wielkość liter i spacje bez znaczenia (KON-015). Kodu organizatora nie ma; żaden kod nie daje roli organizatora (KON-016).
- Do zakończonego turnieju można dołączyć tylko do wglądu; do odwołanego nie (KON-018).
- Zdjęty z meczu sędzia dostaje powiadomienie (MNZ-031).

**Drużyny i składy**
- Limit drużyn co najmniej 1 w formularzu (DRU-006). Nie można zmniejszyć limitu poniżej liczby zaakceptowanych (UST-005).
- Status opłaty tylko informuje, nie blokuje akceptacji (DRU-009).
- Odrzucona drużyna wraca przez ponowne zgłoszenie, bez przycisku „Przywróć” (DRU-007).
- Organizator może zgłaszać drużyny na każdym etapie turnieju (UST-011).
- Zawodnik Kontuzjowany i Zawieszony jest do wyboru w meczu; usunięty ze składu nie (DRU-016).

**Turniej i terminarz**
- Adres publiczny (slug) stały od utworzenia (UST-003).
- Lista turniejów publicznych (`/` dla gościa, stały adres `/t`): turnieje z włączonym portalem i przełącznikiem „Pokazuj na liście turniejów” (domyślnie włączony, także dla istniejących), bez szkiców i odwołanych. Ukryty z listy turniej dalej ma portal pod swoim linkiem. Sekcje wg terminu: Trwają teraz (status „W trakcie” albo mecz na żywo), Nadchodzące (od najbliższego), Zakończone (od ostatnio zakończonego, 6 i „Pokaż więcej”). Filtry: wyszukiwarka (nazwa, organizator), status, termin (dziś, ten weekend, 7 dni, ten miesiąc, własne daty; pasuje, gdy któryś dzień turnieju jest w zakresie), organizator, kategoria; wszystkie w adresie. Zalogowany na `/` trafia do panelu, link „Turnieje publiczne” ma w menu i w stopce; z portalu turnieju prowadzi do listy „Wszystkie turnieje” (PUB-020, 2026-11-10).
- Skrócenie dat turnieju zablokowane, dopóki mecze są poza nowymi datami (UST-004).
- Wyczyszczenie terminarza w formacie z pucharem zostawia drabinkę i jej mecze, znikają terminy (także zaplanowane terminy kolejnych rund) i mecze grupowe (GPK-010, 2026-11-10).
- Terminarz powstaje raz dla całego turnieju, wszystkie kategorie razem. Generator odmawia, dopóki któraś kategoria nie jest gotowa: brak drabinki (puchar, grupy + puchar), jedna drużyna w kategorii albo w grupie (pusta grupa nie przeszkadza). Ta sama drużyna z bazy w kilku kategoriach nigdy nie gra dwóch meczów naraz, także w meczach pucharu, do których może dojść (to ostatnie ustępuje tylko, gdy inaczej mecze się nie mieszczą). Terminarz kończy się jak najwcześniej (boisko nie stoi puste, gdy ktoś może grać), a w tych ramach drużyny mają jak najdłuższe przerwy. Mecze pucharu po grupach dostają termin od razu, po fazie grupowej swoich grup (2026-11-10).
- Zmiana terminu z opcją „Przesuń też kolejne mecze na tym boisku” (domyślnie włączona) przesuwa o tyle samo, w przód i w tył, późniejsze nierozpoczęte mecze tylko na tym boisku; inne boiska zostają. Gdy przy przesunięciu na później przesunięty mecz najeżdża na mecz tej samej drużyny albo sędziego na innym boisku, aplikacja pyta „Przesunąć mimo to?” i po potwierdzeniu przesuwa o tyle samo ten mecz i późniejsze na jego boisku (aż nic nie koliduje). Mecze na innych boiskach bez kolizji i wcześniejsze niż kolidujący zostają. Kolizja z trwającym albo zakończonym meczem, kolizja przy przesuwaniu na wcześniej i wyjście poza daty turnieju odrzucają całą zmianę. Opcji nie ma przy zmianie boiska (2026-11-10).

**Mecz i wyniki**
- Gol bez strzelca dozwolony; strzelca i asystę można uzupełnić później (MNZ-010).
- Ręczna zmiana wyniku dopisuje brakujące gole bez strzelca, a przy obniżeniu cofa najpierw gole bez strzelca. W panelu sędziego nie ma osobnego przycisku: w trakcie meczu są szybkie gole, po meczu „Zmień rozstrzygnięcie” (MNZ-018, 2026-11-09).
- Cofnięcie pierwszej z dwóch żółtych cofa też automatyczną czerwoną (MNZ-015).
- W pucharze korekta nie może zostawić remisu bez karnych, także przy cofaniu goli (PUC-049).
- Organizator zmienia wynik bez limitu czasu do zakończenia turnieju; potem blokada (PUC-076).
- Drużyny nie można wycofać w trakcie jej meczu (PUC-054). Wycofanie z grupy: rozegrane wyniki zostają, nierozegrane mecze odwołane (GRP-020).
- Karne w meczu grupowym nie zmieniają tabeli, są tylko informacją (GRP-026).
- Mecz, którego nikt nie zakończył, kończy się sam z bieżącym wynikiem, gdy od startu minęło 3× czasu meczu i od 4 h nikt nic przy nim nie zrobił (gol, kartka, pauza, wznowienie, zmiana wyniku), więc dogrywka, karne i długa przerwa nie przeszkadzają (2026-11-10; wcześniej tego dnia: 3× czas meczu od startu, min. 2 h). Remis w pucharze system raz wstrzymuje i powiadamia organizatorów, bo bez karnych nie ma zwycięzcy; po tym mecz należy do organizatora i system go już nie rusza (MNZ-040…042, 2026-11-10).
- Usunięcie grupy blokują tylko oczekujące i przyjęte drużyny oraz mecze grupy (także odwołane). Wycofane i odrzucone zgłoszenia tracą wtedy grupę (GRP-004, 2026-11-10).
- Nowa kategoria w formacie z grupami dostaje od razu wybraną liczbę grup (1-8, domyślnie 1); drużyny bez grupy trafiają do Grupy A (GRP-002, 2026-11-10).
- Bez zapisanych reguł remisu punktowego obowiązuje domyślna kolejność: punkty i bilans w meczach bezpośrednich, bilans bramek, gole zdobyte, zwycięstwa. Nie jest zapisywana w bazie, więc działa też dla starych turniejów; zapis własnej kolejności ją zastępuje (GRP-005, 2026-11-10).

**Drabinka i mistrz**
- Korekta wyniku zmieniająca awans: nierozpoczęty mecz kolejnej rundy dostaje nową drużynę sam; rozpoczęty zostaje bez zmian, a organizator dostaje powiadomienie (PUC-048, GPK-008).
- Przy 3 drużynach (półfinał z wolnym losem) nie ma meczu o 3. miejsce; trzecie miejsce zajmuje przegrany rozegranego półfinału (PUC-019).
- Drabinka nie ma górnego limitu drużyn: pierwsza runda to najmniejsza potęga dwójki, w której mieszczą się wszyscy, a wolne losy dostają najwyżej rozstawieni. Do wyboru są wielkości do 1/8 finału i dalej, aż do tej mieszczącej wszystkie drużyny. Zastępuje dawne ostrzeżenie „zmieści się 16 z N” (PUC-020, 2026-11-10).
- Formularz drabinki pozwala wybrać tylko istniejące miejsca w grupie (GPK-007).
- Baner mistrza pojawia się zaraz po finale, nie czeka na „Zakończ turniej” (PUC-045, PUB-011).

## Na później (pomysły)

Do przeniesienia do GitHub Issues z etykietą `pomysł`:

- Logowanie przez Google, 2FA (konfiguracja Keycloaka).
- Płatności online i wpisowe (status opłaty już jest).
- Tabela „na żywo” liczona z trwających meczów.
- Portal: aktualności, galerie, sponsorzy.
- Statystyki rozszerzone (faule, strzały), forma zawodnika, historia sezonów, odznaki.
- Tryb ligi sezonowej.
- Powiadomienia push, czat drużyny.
- Grafiki wyników do mediów społecznościowych, PDF terminarza.
- Skalowanie: Redis dla SignalR, broker wiadomości zamiast outboxa, wydzielenie usług.
