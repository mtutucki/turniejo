# Turniejo — frontend

Next.js 16 (App Router) · TypeScript · Tailwind + shadcn/ui (New York) · Auth.js · TanStack Query · Zustand

## Uruchomienie lokalne

1. Infrastruktura i backend:
   ```bash
   cd ../Turniejo-backend && docker compose up -d
   cd src/Turniejo.Api && dotnet run --launch-profile http   # http://localhost:5264
   ```
2. Frontend:
   ```bash
   cp .env.example .env.local   # uzupełnij NEXTAUTH_SECRET (npx auth secret)
   npm install
   npm run dev                  # http://localhost:3000
   ```

Keycloak musi mieć na kliencie `turniejo-frontend` włączone _Direct access grants_ oraz mapper
audience `turniejo-api` (oba są w `infra/keycloak/turniejo-realm.json`; przy istniejącym wolumenie
Keycloaka realm się nie reimportuje — dodaj mapper ręcznie w konsoli admina albo odtwórz wolumen).

## Architektura

- **Logowanie** — własny formularz `/login`; Auth.js `CredentialsProvider` wymienia email i hasło
  na tokeny Keycloaka po stronie serwera (`grant_type=password`, `src/lib/auth/keycloak.ts`).
  Tokeny siedzą w szyfrowanym ciasteczku sesji, refresh w callbacku `jwt` (`src/auth.ts`).
- **Rejestracja / reset hasła** — przez backend (`/api/auth/register`, `/api/auth/password-reset/request`);
  po rejestracji automatyczne logowanie i przejście na `/tournaments`.
- **Ochrona stron** — `src/proxy.ts` + `src/lib/auth/public-paths.ts`.
- **API** — przeglądarka woła backend przez rewrite `/backend/*` (backend nie ma CORS).
  `useApi()` dokleja Bearer token; globalna obsługa błędów w `src/lib/api/error-handler.ts`
  (401/403 → logowanie lub toast, 409, 400/422 → pola formularza, 429 z odliczaniem).
- **Role per zasób** — `useAuthStore` (Zustand) wypełniany przez `<AuthSync>` z `GET /api/me/roles`;
  `useMyRoles({ resourceType, resourceId })` i `<RoleGate allow={[UserRole.Organizer]}>`.
- **Design tokens** — paleta z planu w `src/app/globals.css` (Tailwind v4 trzyma motyw w CSS,
  bez `tailwind.config.ts`): `bg-background`, `bg-card`, `bg-brand`, `text-primary`, `bg-live` …

## Skrypty

`npm run dev` · `npm run build` · `npm run lint` · `npm run typecheck` · `npm run format` ·
`npm test` · `npm run test:e2e`

## Testy

- **Komponenty** — Vitest + React Testing Library (`src/**/*.test.tsx`, konfiguracja `vitest.config.mts`):
  `npm test`. Animacje (motion) są w testach wyłączone (`src/test/setup.ts`).
- **E2E** — Playwright (`e2e/*.spec.ts`), projekty `mobile` (Pixel 7) i `desktop`: `npm run test:e2e`.
  Nie potrzebują .NET ani Keycloaka: Playwright startuje mock backendu z handlerów MSW
  (`e2e/mock`, port 4010 — token endpoint Keycloaka, API, SignalR po WebSocketach) i produkcyjny build
  Next.js w `.next-e2e` (port 3100) z `API_URL`/`KEYCLOAK_URL` wskazującymi na mock.
  Stan mocka resetuje się przed każdym testem; `/__control/*` pozwala np. „strzelić gola” z zewnątrz
  i sprawdzić aktualizację na żywo. Wymaga zainstalowanego Google Chrome (`channel: "chrome"`).

