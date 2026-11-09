# Jak pracujemy

Ten sam przebieg dla każdej funkcji i poprawki, niezależnie od tego, czy pisze ją człowiek, czy agent.

## 1. Pomysł → issue

Każda praca zaczyna się od issue na GitHubie (szablony: funkcja, błąd). W issue:

- **co i dla kogo** - jedno, dwa zdania z perspektywy użytkownika (organizator, sędzia, trener, rodzic, kibic),
- **kryteria akceptacji** - lista sprawdzalnych zdań, np. „Sędzia, który nie prowadzi meczu, nie może go rozpocząć (403)”,
- **decyzje** - jeśli coś wymaga wyboru (np. czy karne liczą się do tabeli), decyzja trafia do [decyzje.md](decyzje.md).

Etykiety: `funkcja`, `błąd`, `dług` (porządki techniczne), `pomysł` (na później, bez zobowiązań).

## 2. Branch i plan

- Branch od aktualnego `master`: `feat/krotka-nazwa`, `fix/krotka-nazwa`, `chore/…`.
- Krótki plan: które moduły backendu, które ekrany, jakie testy na każde kryterium.

## 3. Kod i testy razem

- Każde kryterium akceptacji = co najmniej jeden test na właściwej warstwie ([testy.md](testy.md)): reguła serwera → test integracyjny, zachowanie ekranu → E2E na mocku, kluczowa ścieżka przez cały system → pełny stos.
- Poprawka błędu: najpierw test, który pokazuje błąd (czerwony), potem poprawka (zielony).
- Jeden commit na jedną spójną zmianę, opis mówi „dlaczego”.

## 4. Kontrola przed oddaniem

```bash
bash scripts/check.sh --e2e
```

Wszystko zielone. Nowe komunikaty backendu mają polskie tłumaczenie. Nowe teksty w UI są krótkie i bez „—”.

## 5. Pull request

- PR do `master` z wypełnionym szablonem (co, dlaczego, jak sprawdzone, numer issue).
- CI uruchamia: testy backendu, frontendu, E2E na mocku i na pełnym stosie. Merge tylko przy zielonym CI.
- Po merge deploy na serwer idzie sam ([../deploy/README.md](../deploy/README.md)).

## 6. Przed większym wydaniem

Raz na jakiś czas, przed pokazaniem aplikacji ludziom: skill `przeglad-przed-wydaniem`. To krótki ręczny przegląd tego, czego automat nie oceni: wygląd, wygoda na prawdziwym telefonie, czytelność tekstów.

## Z agentem (Claude Code)

- „Zrób issue #12” albo „dodaj funkcję X” - agent prowadzi kroki 2-4 według skilla `nowa-funkcja` i zatrzymuje się na decyzjach produktowych.
- Agent nie pushuje i nie zakłada PR bez prośby.
- Zasady dla agenta: [../CLAUDE.md](../CLAUDE.md) i `CLAUDE.md` w folderach backendu i frontendu.
