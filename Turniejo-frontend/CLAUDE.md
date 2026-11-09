@AGENTS.md

# Frontend: konwencje

Next.js 16 (App Router), TypeScript, TanStack Query, Auth.js (Keycloak), Tailwind + shadcn/ui. Ogólne zasady pracy: [../CLAUDE.md](../CLAUDE.md).

## Struktura

- `src/app/(app)` - panel po zalogowaniu (organizator, sędzia, trener, rodzic), `src/app/(live)` - panel meczu na żywo, `src/app/t/[slug]` - publiczny portal kibica (SSR), `src/app/(auth)` - logowanie, rejestracja, reset hasła.
- `src/features/<obszar>/` - logika obszaru: `api.ts` (hooki React Query do backendu), `components/`, czyste funkcje z testami obok (`*.test.ts`).
- `src/lib/api/` - klient API, obsługa błędów, `messages.ts` (tłumaczenia komunikatów backendu na polski), `form-errors.ts` (błędy pól z 400/422 do formularza).
- `src/types/` - typy 1:1 z DTO backendu (komentarz z nazwą DTO nad typem).
- `src/components/ui/` - komponenty shadcn (nie edytuj bez potrzeby).

## Zasady

- Dane z backendu tylko przez hooki w `features/*/api.ts`. Po mutacji unieważnij zapytania, które pokazują zmienione dane.
- Realtime (SignalR) przez istniejące hooki (`use-match-realtime`, `use-tournament-matches-realtime`) - nie otwieraj nowych połączeń w komponentach.
- Formularze: react-hook-form + zod. Reguły walidacji 1:1 z walidatorem backendu (komentarz, z którym). Mutacja z błędami pól ma `meta: { handlesValidation: true }` i `onError: (e) => applyFieldErrors(e, form)`.
- Każdy nowy komunikat backendu dostaje polskie tłumaczenie w `lib/api/messages.ts` (sprawdza to `messages.test.ts`).
- Przyciski zmieniające stan (start meczu, zapis) blokuj od pierwszego kliknięcia - podwójne kliknięcie na telefonie to częsty błąd.
- Aplikacja jest mobile-first (sędzia na boisku, kibic na trybunach): nic nie może przewijać się w bok na 412 px szerokości.
- Teksty UI: patrz „Teksty w aplikacji” w [../CLAUDE.md](../CLAUDE.md).

## Testy

- Vitest (`npm test`) - czyste funkcje (`*.test.ts` obok kodu).
- Playwright na mocku (`npm run test:e2e`) - `e2e/*.spec.ts` na produkcyjnym buildzie z mockiem backendu (`e2e/mock`, MSW). Test może podmienić odpowiedź API (`mock.override`) i policzyć wywołania (`mock.calls`). Brakujący endpoint w mocku: dopisz handler w `e2e/mock/handlers.ts` (stan w `e2e/mock/db.ts`). Projekty: `mobile` (Pixel 7) i `desktop`.
- Playwright na pełnym stosie (`e2e-full/`) - prawdziwy backend i Keycloak w Dockerze, tylko kluczowe przepływy. Dane przez API (`e2e-full/api.ts`).
- Tytuł testu zaczyna się od ID scenariusza w nawiasie, np. `test("[PUC-013] …")`.

```bash
npm run lint && npm run typecheck && npm test
npm run test:e2e                     # buduje aplikację i stawia mock sam (pierwszy raz kilka minut)
npx playwright test e2e/live.spec.ts --project=mobile
```
