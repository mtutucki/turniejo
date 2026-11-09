---
name: przeglad-przed-wydaniem
description: Krótki ręczny przegląd Turniejo w przeglądarce przed pokazaniem wydania ludziom - wygląd, wygoda na telefonie, teksty, czyli to, czego testy automatyczne nie oceniają. Użyj, gdy użytkownik prosi o przegląd, sprawdzenie przed wydaniem albo „QA”.
---

# Przegląd przed wydaniem

Testy automatyczne (zob. [docs/testy.md](../../../docs/testy.md)) sprawdzają reguły i zachowanie. Ten przegląd dotyczy tylko tego, czego nie oceniają: czy jest czytelnie, wygodnie i ładnie. Nie powtarzaj scenariuszy, które mają testy.

## Przygotowanie

1. Zapytaj, gdzie przegląd: lokalnie (`dev.bat`) albo na stosie testowym (`Turniejo-frontend`: `npm run e2e:full:up`, adres http://localhost:13100). Nigdy na produkcji.
2. Upewnij się, że `bash scripts/check.sh --e2e` jest zielone - przegląd ręczny nie zastępuje testów.
3. Dane: utwórz nowy turniej `QA-<data>` i konta `qa+…@test.pl`. Nie używaj resetu hasła lokalnie (prawdziwy SMTP). Niczego nie usuwaj bez zgody.
4. Przeglądarka: Playwright MCP, okno telefonu 412×915 oraz desktop 1440×900.

## Co obejrzeć

Dla każdej roli (organizator, sędzia, trener, rodzic, kibic bez logowania) przejdź główną ścieżkę zmienionego obszaru i sprawdź:

- **Telefon:** nic nie wychodzi poza ekran, przyciski łatwo trafić kciukiem, dialogi mieszczą się, klawiatura nie zasłania pól.
- **Teksty:** po polsku, krótkie, bez „—”, bez angielskich komunikatów i technicznych szczegółów.
- **Stany:** pusty widok, ładowanie, błąd, długie nazwy (drużyna ponad 50 znaków), polskie znaki.
- **Kilka ról naraz:** konto z rolą sędziego i trenera widzi obie.
- **Na żywo:** dwa okna (sędzia i kibic) - wynik zmienia się bez odświeżania.

## Raport

Dla każdej uwagi: rola, ekran, telefon czy desktop, co nie tak, zrzut ekranu (`qa-przeglad/<data>/`, poza repo). Podziel na: **błąd** (zgłoś jako issue z szablonu `Błąd`, jeśli użytkownik zgodzi się), **do poprawy wyglądu**, **pomysł**. Na koniec lista założonych turniejów i kont testowych (do ewentualnego sprzątnięcia za zgodą).
