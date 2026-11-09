# Turniejo

Aplikacja do prowadzenia turniejów piłkarskich dla dzieci i amatorów: zapisy drużyn, składy, terminarz, grupy i drabinka pucharowa, mecze prowadzone na żywo przez sędziów, panele trenera i rodzica oraz publiczny portal kibica.

| Część | Technologie | Folder |
|---|---|---|
| Backend | .NET 9, modularny monolit, CQRS (MediatR), EF Core, PostgreSQL, SignalR | [Turniejo-backend](Turniejo-backend) |
| Frontend | Next.js 16, React, TanStack Query, Auth.js, Tailwind + shadcn/ui | [Turniejo-frontend](Turniejo-frontend) |
| Logowanie | Keycloak (konta, hasła, tokeny JWT) | `Turniejo-backend/infra/keycloak` |
| Wdrożenie | Docker Compose + Caddy na VPS, deploy z GitHub Actions po merge do `master` | [deploy](deploy) |

## Uruchomienie lokalne

1. Infrastruktura (Postgres, Keycloak, Mailpit, pgAdmin):
   ```bash
   cd Turniejo-backend && docker compose -p turniejo up -d
   ```
2. Backend i frontend naraz: `dev.bat` (Windows), albo osobno:
   ```bash
   dotnet run --project Turniejo-backend/src/Turniejo.Api      # http://localhost:5264
   npm --prefix Turniejo-frontend run dev                       # http://localhost:3000
   ```
   Pierwszy raz: `Turniejo-frontend/.env.local` z `.env.example` oraz `appsettings.Development.json` w API (sekrety, poza repo).

Podgląd maili z aplikacji: http://localhost:8025 (Mailpit). Panel Keycloaka: http://localhost:8080 (admin/admin, tylko dev).

## Testy

Szczegóły i zasady: [docs/testy.md](docs/testy.md). Najczęściej:

```bash
bash scripts/check.sh          # lint, typy, testy jednostkowe frontendu i wszystkie testy backendu
bash scripts/check.sh --e2e    # to samo + E2E na mocku (ok. 6 min)
```

## Dokumentacja

- [docs/jak-pracujemy.md](docs/jak-pracujemy.md) - jak dodajemy funkcje i poprawki, krok po kroku
- [docs/architektura.md](docs/architektura.md) - moduły, przepływ danych, czasu rzeczywistego i uprawnień
- [docs/testy.md](docs/testy.md) - warstwy testów, kiedy który, jak uruchomić
- [docs/decyzje.md](docs/decyzje.md) - decyzje architektoniczne i produktowe
- [docs/wersjonowanie.md](docs/wersjonowanie.md) - numery wersji, lista zmian, wydania ([CHANGELOG.md](CHANGELOG.md))
- [deploy/README.md](deploy/README.md) - serwer, wdrożenie, backup
- [CLAUDE.md](CLAUDE.md) - instrukcje dla agenta (Claude Code)
