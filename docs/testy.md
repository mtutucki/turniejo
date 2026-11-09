# Testy

Testy są specyfikacją aplikacji: każde wymaganie ma test, a nazwa testu mówi, co sprawdza. Stary ręczny arkusz QA (307 scenariuszy) jest w całości zautomatyzowany i zarchiwizowany poza repo - jego ID (np. `PUC-052`) zostały w nazwach testów.

## Warstwy

| Warstwa | Gdzie | Co sprawdza | Kiedy jej użyć |
|---|---|---|---|
| Jednostkowe backend | `Turniejo-backend/tests/Turniejo.<Moduł>.UnitTests` | logika domeny, handlery w izolacji | obliczenia, reguły encji, przypadki brzegowe |
| Integracyjne backend | `Turniejo-backend/tests/Turniejo.IntegrationTests` | API + prawdziwa baza (Testcontainers) | każda reguła biznesowa i uprawnienie widoczne przez API |
| Architektura | `Turniejo-backend/tests/Turniejo.Architecture.UnitTests` | granice modułów, oznaczenia uprawnień | działa samo, nie dopisujesz |
| Jednostkowe frontend | `Turniejo-frontend/src/**/*.test.ts` (Vitest) | czyste funkcje UI (np. propozycja par, tłumaczenia) | logika liczona w przeglądarce |
| E2E na mocku | `Turniejo-frontend/e2e` (Playwright + MSW) | ekrany, formularze, komunikaty, role, telefon | zachowanie interfejsu |
| Pełny stos | `Turniejo-frontend/e2e-full` (Playwright + Docker) | cały system razem: Keycloak, SignalR, baza | tylko kluczowe przepływy (dziś 4) |

Zasada: test jak najniżej, jak się da. Reguła „nie można zmienić formatu po wygenerowaniu terminarza” to test integracyjny, nie E2E. E2E sprawdza, co widzi użytkownik.

## Uruchamianie

```bash
bash scripts/check.sh            # backend (wszystkie testy) + frontend: lint, typy, Vitest
bash scripts/check.sh --e2e      # + E2E na mocku (oba widoki: telefon i desktop)

# pojedynczo, w trakcie pracy (sekundy)
dotnet test Turniejo-backend/Turniejo.slnx --filter "FullyQualifiedName~GroupStageRules"
cd Turniejo-frontend && npx playwright test e2e/bracket.spec.ts --project=mobile
cd Turniejo-frontend && npx vitest run src/lib/version.test.ts

# pełny stos (Docker, ok. 6 min budowy obrazów za pierwszym razem)
cd Turniejo-frontend
npm run e2e:full:up && npm run test:e2e:full && npm run e2e:full:down
```

Testy integracyjne i pełny stos wymagają działającego Dockera. Przy uruchomionym API testy backendu dodaj `--artifacts-path "$TEMP/turniejo-test"`.

## Pomocnicy

- **Backend:** `TournamentScenario.GroupsAsync(...)` / `KnockoutAsync(...)` - gotowy turniej z drużynami, terminarzem i startem jednym wywołaniem; metody `PlayAsync`, `JoinAsync("Referee")`, `CreatePlayerAsync`, `NotificationsAsync`.
- **Uprawnienia:** `PermissionMatrixTests` - każdy endpoint zmieniający dane × 9 ról. Nowy endpoint dopisz do macierzy.
- **E2E na mocku:** `mock.override({ method, path, status, body })` podmienia odpowiedź API na czas testu, `mock.calls("POST", "/finish")` liczy wywołania, `mock.tournament(id, {...})` i `mock.matchStatus(...)` ustawiają stan.
- **Pełny stos:** `Scenario.knockout(4)` / `Scenario.groups(2, 2)` w `e2e-full/api.ts` - dane przez prawdziwe API, konta `qa+…@test.pl`.

## Nazewnictwo

- Backend: `ID_Opis_JakoZdanie`, np. `PUC_052_WithdrawingATeamDuringTheTournament_GivesTheRivalAWalkover`.
- Frontend: `test("[ID] opis po polsku", …)`.
- Nowe wymagania bez ID ze starego arkusza: numer issue zamiast ID, np. `GH_27_…` / `[#27]`.

## CI

`.github/workflows/deploy.yml`: testy backendu, frontendu, E2E na mocku i pełny stos. Deploy rusza dopiero, gdy wszystko jest zielone. Raporty Playwrighta przy błędzie są w artefaktach joba.
