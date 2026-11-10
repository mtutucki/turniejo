# Jak pracujemy

Ten sam przebieg dla każdej funkcji i poprawki, niezależnie od tego, czy pisze ją człowiek, czy agent.

## 1. Pomysł → issue

Każda praca zaczyna się od issue na GitHubie (szablony: funkcja, błąd). W issue:

- **co i dla kogo** - jedno, dwa zdania z perspektywy użytkownika (organizator, sędzia, trener, rodzic, kibic),
- **kryteria akceptacji** - lista sprawdzalnych zdań, np. „Sędzia, który nie prowadzi meczu, nie może go rozpocząć (403)”,
- **decyzje** - jeśli coś wymaga wyboru (np. czy karne liczą się do tabeli), decyzja trafia do [decyzje.md](decyzje.md).

Etykiety: `funkcja`, `błąd`, `dług` (porządki techniczne), `pomysł` (na później, bez zobowiązań).

## 2. Branch i plan

Kilka funkcji i poprawek wychodzi razem jako jedno wydanie. Zbieramy je na branchu wydania `release/X.Y.Z`, a do `master` trafia jeden PR na wydanie. Dzięki temu pełna kontrola, PR i deploy są raz na kilka zmian, a nie przy każdej.

- Początek cyklu: `bash scripts/release.sh start 1.5.0` zakłada `release/1.5.0` od `master`. Numer według [wersjonowanie.md](wersjonowanie.md) (zawsze można go zmienić później, zakładając nowy branch).
- Każda funkcja i poprawka: branch od `release/1.5.0`: `feat/…`, `fix/…`, `chore/…`. Nazwa po angielsku, krótka, z myślnikami, np. `feat/whats-new`, `fix/bracket-third-place`.
- Gotowa zmiana wraca merge'em lokalnie: `git switch release/1.5.0 && git merge --no-ff feat/whats-new`. Bez PR i bez pushu.
- Krótki plan: które moduły backendu, które ekrany, jakie testy na każde kryterium.

## 3. Kod i testy razem

- Każde kryterium akceptacji = co najmniej jeden test na właściwej warstwie ([testy.md](testy.md)): reguła serwera → test integracyjny, zachowanie ekranu → E2E na mocku, kluczowa ścieżka przez cały system → pełny stos.
- Poprawka błędu: najpierw test, który pokazuje błąd (czerwony), potem poprawka (zielony).
- W trakcie pracy uruchamiaj tylko testy tego, co zmieniasz: jeden plik E2E, `dotnet test --filter …`, `npx vitest run plik` (przykłady w [testy.md](testy.md#uruchamianie)). To sekundy, a nie minuty.
- Jeden commit na jedną spójną zmianę, opis mówi „dlaczego”.
- Zmiana widoczna dla użytkownika: jedna linijka w sekcji „Nieopublikowane” w [../CHANGELOG.md](../CHANGELOG.md).

## 4. Kontrola przed PR wydania

Pełna kontrola raz na wydanie, na `release/X.Y.Z` tuż przed pushem i założeniem PR (trwa kilka minut). Nie uruchamiaj jej po skończeniu funkcji ani po merge'u do brancha wydania, wystarczą testy zmienianego miejsca. CI po pushu i tak puszcza wszystko jeszcze raz, łącznie z pełnym stosem.

```bash
bash scripts/check.sh --e2e
```

Wszystko zielone. Nowe komunikaty backendu mają polskie tłumaczenie. Nowe teksty w UI są krótkie i bez „—”.

## 5. Wydanie i pull request

- Na `release/1.5.0`: `bash scripts/release.sh prepare 1.5.0` („Nieopublikowane” w CHANGELOG staje się [1.5.0]), pełna kontrola, push.
- Jeden PR `release/1.5.0` → `master` z wypełnionym szablonem: lista funkcji i poprawek, jak sprawdzone, `Closes #N` dla każdego issue.
- CI uruchamia: testy backendu, frontendu, E2E na mocku i na pełnym stosie. Merge tylko przy zielonym CI.
- Po merge deploy na serwer idzie sam ([../deploy/README.md](../deploy/README.md)), potem `bash scripts/release.sh tag 1.5.0` (GitHub Release i numer wersji na serwerze).
- Pilna poprawka w trakcie cyklu: osobne mini-wydanie od `master` ([wersjonowanie.md](wersjonowanie.md#pilna-poprawka)).

Zasady numeracji i szczegóły wydania: [wersjonowanie.md](wersjonowanie.md).

## 6. Przegląd przed wydaniem

Przed większym wydaniem warto przejść skill `przeglad-przed-wydaniem`: krótki ręczny przegląd tego, czego automat nie oceni (wygląd, wygoda na prawdziwym telefonie, czytelność tekstów).

## Z agentem (Claude Code)

- „Zrób issue #12” albo „dodaj funkcję X” - agent prowadzi kroki 2-3 według skilla `nowa-funkcja` i zatrzymuje się na decyzjach produktowych. Po akceptacji mergeuje zmianę do brancha wydania.
- Agent nie pushuje i nie zakłada PR bez prośby. Pełną kontrolę uruchamia tylko przy PR wydania.
- Zasady dla agenta: [../CLAUDE.md](../CLAUDE.md) i `CLAUDE.md` w folderach backendu i frontendu.
