---
name: nowa-funkcja
description: Prowadzi funkcję albo poprawkę w Turniejo od opisu do gotowej zmiany - kryteria, plan, kod, testy na właściwej warstwie, kontrola. Użyj, gdy użytkownik prosi o nową funkcję, zmianę zachowania, poprawkę błędu albo „zrób issue #N”.
---

# Nowa funkcja / poprawka w Turniejo

Pracujesz według [docs/jak-pracujemy.md](../../../docs/jak-pracujemy.md) i zasad z [CLAUDE.md](../../../CLAUDE.md). Kroki poniżej wykonuj po kolei.

## 1. Zrozum zadanie

- Jeśli jest numer issue: przeczytaj je (`gh issue view N`). Jeśli nie ma: zbierz od użytkownika „co i dla kogo”.
- Spisz kryteria akceptacji jako sprawdzalne zdania. Pokaż je użytkownikowi w 3-6 punktach.
- Decyzje produktowe (np. „czy karne liczą się do tabeli”) - zapytaj z rekomendacją. Nie zgaduj. Sprawdź najpierw [docs/decyzje.md](../../../docs/decyzje.md), czy decyzja już zapadła.

## 2. Plan

- Branch od bieżącego brancha wydania `release/X.Y.Z` (`git branch --list 'release/*'`): `feat/…` albo `fix/…`, nazwa po angielsku, krótka, z myślnikami (`feat/whats-new`). Nie ma brancha wydania: zaproponuj numer (nowa funkcja → Y, same poprawki → Z, [docs/wersjonowanie.md](../../../docs/wersjonowanie.md)) i po zgodzie `bash scripts/release.sh start X.Y.Z`.
- Przeczytaj `CLAUDE.md` części, które zmieniasz (backend, frontend).
- Krótki plan dla użytkownika: moduły backendu, ekrany, test na każde kryterium (warstwa wg [docs/testy.md](../../../docs/testy.md)).

## 3. Kod i testy

- Poprawka błędu: najpierw test, który pokazuje błąd, potem poprawka.
- Backend: nowy command/query z oznaczeniem uprawnień; nowy endpoint zmieniający dane dopisz do `PermissionMatrixTests`; regułę sprawdź testem integracyjnym w `tests/Turniejo.IntegrationTests/Rules` (pomocnik `TournamentScenario`).
- Frontend: hook w `features/*/api.ts`, komunikaty backendu przetłumaczone w `lib/api/messages.ts`, ekran sprawdzony testem E2E na mocku (brakujący endpoint dopisz do `e2e/mock/handlers.ts` albo użyj `mock.override`). Sprawdź telefon (projekt `mobile`).
- Migracja bazy: komenda w `Turniejo-backend/CLAUDE.md`.
- Nazwy testów z numerem issue: `GH_27_…` (backend), `test("[#27] …")` (frontend).
- W trakcie pracy uruchamiaj tylko testy zmienianego miejsca (jeden plik E2E, `dotnet test --filter …`, `npx vitest run plik`). Pełnej kontroli nie puszczaj po każdej zmianie.
- Jeden commit na spójną zmianę.

## 4. Dokumenty

Nowe decyzje dopisz do `docs/decyzje.md`, zmiany w architekturze do `docs/architektura.md`. Zmiana widoczna dla użytkownika: jedna linijka w sekcji „Nieopublikowane” w `CHANGELOG.md` (kategoria Nowe / Zmienione / Poprawione / Usunięte, po polsku, z perspektywy użytkownika).

## 5. Oddanie

Krótko dla użytkownika: co działa, jak to sprawdzono, co zostało (jeśli coś). Pełnej kontroli tu nie uruchamiaj. Po akceptacji: merge lokalnie do brancha wydania (`git switch release/X.Y.Z && git merge --no-ff feat/…`). Bez pushu i bez PR.

## 6. Wydanie (tylko na prośbę użytkownika)

Na `release/X.Y.Z`: `bash scripts/release.sh prepare X.Y.Z`, potem pełna kontrola, raz:

```bash
bash scripts/check.sh --e2e
```

Wszystko zielone. Jeśli w wydaniu jest zmiana kluczowego przepływu (logowanie, mecz na żywo, drabinka, portal), także pełny stos (`Turniejo-frontend`: `npm run e2e:full:up && npm run test:e2e:full && npm run e2e:full:down`). Potem push i jeden PR do `master`, opis po angielsku według `.github/pull_request_template.md`, z listą zmian i `Closes #N` dla każdego issue. Po merge: `bash scripts/release.sh tag X.Y.Z`.
