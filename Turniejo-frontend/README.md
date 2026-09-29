# Turniejo — frontend

Next.js 16 (App Router) · TypeScript · Tailwind + shadcn/ui · Auth.js (Keycloak) · TanStack Query

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

Keycloak musi mieć na kliencie `turniejo-frontend` mapper audience `turniejo-api`
(jest w `infra/keycloak/turniejo-realm.json`; przy istniejącym wolumenie Keycloaka realm się
nie reimportuje — dodaj mapper ręcznie w konsoli admina albo odtwórz wolumen).

## Architektura

- **Logowanie** — Authorization Code + PKCE bezpośrednio w Keycloaku (`src/auth.ts`),
  sesja w szyfrowanym ciasteczku JWT, automatyczny refresh tokenu.
- **Ochrona stron** — `src/proxy.ts` + `src/lib/auth/public-paths.ts`.
- **API** — przeglądarka woła backend przez rewrite `/backend/*` (backend nie ma CORS).
  `useApi()` dokleja Bearer token; globalna obsługa błędów w `src/lib/api/error-handler.ts`
  (401/403 → logowanie lub toast, 409, 422/400, 429 z odliczaniem).
- **Role per zasób** — `useMyRoles({ resourceType, resourceId })` i `<RoleGate>`
  (dane z `GET /api/me/roles`).

## Skrypty

`npm run dev` · `npm run build` · `npm run lint` · `npm run typecheck` · `npm run format`
