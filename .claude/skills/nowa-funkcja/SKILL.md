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

- Branch od aktualnego `master`: `feat/…` albo `fix/…` (zapytaj, jeśli użytkownik pracuje na innym).
- Przeczytaj `CLAUDE.md` części, które zmieniasz (backend, frontend).
- Krótki plan dla użytkownika: moduły backendu, ekrany, test na każde kryterium (warstwa wg [docs/testy.md](../../../docs/testy.md)).

## 3. Kod i testy

- Poprawka błędu: najpierw test, który pokazuje błąd, potem poprawka.
- Backend: nowy command/query z oznaczeniem uprawnień; nowy endpoint zmieniający dane dopisz do `PermissionMatrixTests`; regułę sprawdź testem integracyjnym w `tests/Turniejo.IntegrationTests/Rules` (pomocnik `TournamentScenario`).
- Frontend: hook w `features/*/api.ts`, komunikaty backendu przetłumaczone w `lib/api/messages.ts`, ekran sprawdzony testem E2E na mocku (brakujący endpoint dopisz do `e2e/mock/handlers.ts` albo użyj `mock.override`). Sprawdź telefon (projekt `mobile`).
- Migracja bazy: komenda w `Turniejo-backend/CLAUDE.md`.
- Nazwy testów z numerem issue: `GH_27_…` (backend), `test("[#27] …")` (frontend).
- Jeden commit na spójną zmianę.

## 4. Kontrola

```bash
bash scripts/check.sh                                   # backend + lint, typy, Vitest
cd Turniejo-frontend && npx playwright test e2e/<nowe-i-zmienione>.spec.ts
```

Wszystko zielone. Całe E2E (`bash scripts/check.sh --e2e`) dopiero przed PR, gdy użytkownik o niego prosi. Jeśli zmiana dotyka kluczowego przepływu (logowanie, mecz na żywo, drabinka, portal), przed PR także pełny stos (`Turniejo-frontend`: `npm run e2e:full:up && npm run test:e2e:full && npm run e2e:full:down`).

Nowe decyzje dopisz do `docs/decyzje.md`, zmiany w architekturze do `docs/architektura.md`. Zmiana widoczna dla użytkownika: jedna linijka w sekcji „Nieopublikowane” w `CHANGELOG.md` (kategoria Nowe / Zmienione / Poprawione / Usunięte, po polsku, z perspektywy użytkownika).

## 5. Oddanie

Krótko dla użytkownika: co działa, jak to sprawdzono, co zostało (jeśli coś). Nie pushuj i nie zakładaj PR bez prośby. Na prośbę najpierw `bash scripts/check.sh --e2e`, potem opis PR po angielsku według `.github/pull_request_template.md`, z `Closes #N`.
