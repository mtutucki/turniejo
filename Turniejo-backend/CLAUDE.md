# Backend: konwencje

.NET 9, modularny monolit. Ogólne zasady pracy: [../CLAUDE.md](../CLAUDE.md). Architektura: [../docs/architektura.md](../docs/architektura.md).

## Struktura

- `src/Turniejo.Api` - kontrolery (cienkie: request → command/query → `ToActionResult()`), middleware, hosting, background services.
- `src/Shared/Turniejo.SharedKernel` - `Result`/`Error`, CQRS, behaviory MediatR, kontrakty między modułami (`Contracts/I*Module.cs`), outbox.
- `src/Modules/<Moduł>/Turniejo.<Moduł>.{Domain,Application,Infrastructure}` - moduły: Users, Auth, Organizations, Tournaments, Teams, Players, Matches, Standings, Brackets, Notifications.
  - Domain: encje, enumy, zdarzenia domenowe. Bez zależności od EF i MediatR.
  - Application: `Folder/NazwaCommand.cs` + `Handler` + `Validator`, DTO, usługi, handlery zdarzeń z innych modułów.
  - Infrastructure: `DbContext` (własny schemat w Postgresie), konfiguracje EF, migracje w `Persistence/Migrations`, implementacja `I<Moduł>Module`.

## Zasady

- **Granice modułów:** moduł nie sięga do bazy innego modułu. Synchronicznie - przez interfejs z `SharedKernel/Contracts`. Asynchronicznie - zdarzenie zapisane w outboxie (`OutboxMessage`) i obsłużone `INotificationHandler` w innym module. Brak kluczy obcych między schematami.
- **Uprawnienia:** każdy command/query implementuje dokładnie jedno: `IAuthorizedRequest` (rola w zakresie, np. Organizer w Tournament; opcjonalnie `IAlternateAuthorizedScope`, np. sędzia meczu), `IAuthenticatedOnlyRequest` (handler sam sprawdza dostęp, np. `PlayerAccessGuard`) albo `IPublicRequest`. Pilnuje tego test architektury. Gdy handler bierze identyfikator zasobu z requestu, sprawdź, że zasób należy do zakresu z autoryzacji (inaczej organizator turnieju B dosięgnie danych turnieju A) - macierz w `PermissionMatrixTests` to łapie.
- **Błędy:** `Result.Failure(Error.Validation|NotFound|Conflict(...))` → 400/404/409; walidator FluentValidation → 422; `UnauthorizedException` → 403. Komunikat po angielsku, jednym zdaniem - frontend tłumaczy go w `lib/api/messages.ts`.
- **Czas:** `DateTimeOffset` w UTC. Czas w treściach dla ludzi (powiadomienia) w strefie Europe/Warsaw (`NotificationTime`).
- **Wynik i tabela to projekcje:** wynik meczu liczy się ze zdarzeń (`MatchEvent`), tabela ze zakończonych meczów, drabinka z wyników. Zmieniasz zdarzenia - przelicz projekcję, nie poprawiaj jej ręcznie.
- **Współbieżność zapisu:** formularze, które może edytować kilku organizatorów, wysyłają wersję albo wczytane wartości (`ExpectedUpdatedAt`, `Expected`) i dostają 409 przy konflikcie.

## Migracje

Bez fabryki design-time - startup to API (wymaga `appsettings.Development.json` z connection stringiem). Zatrzymaj API przed generowaniem.

```bash
dotnet ef migrations add NazwaMigracji \
  --project src/Modules/Matches/Turniejo.Matches.Infrastructure \
  --startup-project src/Turniejo.Api \
  --context MatchesDbContext --output-dir Persistence/Migrations
```

Migracje stosują się same przy starcie (`Database:MigrateOnStartup`). Enumy zapisujemy jako `int` - nowa wartość na końcu enuma nie wymaga migracji.

## Testy

- `tests/Turniejo.<Moduł>.UnitTests` - logika domeny i handlery z NSubstitute.
- `tests/Turniejo.IntegrationTests` - prawdziwe API + Postgres w Testcontainers (wymaga Dockera). `Rules/` - reguły biznesowe po jednym pliku na obszar, nazwy testów zaczynają się od ID scenariusza (np. `PUC_052_…`). `Scenarios/` - dłuższe przepływy i macierz uprawnień. Dane buduje `Infrastructure/TournamentScenario` (gotowy turniej grupowy albo pucharowy jednym wywołaniem).
- `tests/Turniejo.Architecture.UnitTests` - granice i oznaczenia uprawnień.

```bash
dotnet test Turniejo.slnx
dotnet test tests/Turniejo.IntegrationTests --filter "FullyQualifiedName~KnockoutRules"
```
