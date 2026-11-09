import { expect, type Page, test as base } from "@playwright/test"

export const MOCK_URL = "http://localhost:4010"
export const PASSWORD = "Haslo1234!"
export const ORGANIZER = "organizator@e2e.pl"
export const REFEREE = "sedzia@e2e.pl"
export const PUBLIC_SLUG = "puchar-e2e"
export const MATCH_ID = "0f000000-0000-4000-8000-000000000060"
export const VIEWER_CODE = "EE3333"

type MockState = {
  tournaments: { id: string; name: string; status: string }[]
  matches: { id: string; homeScore: number; awayScore: number }[]
  users: { email: string; roles: string[] }[]
  organizations: string[]
  connections: { tournaments: number; matches: number }
}

export const mock = {
  state: async (): Promise<MockState> => (await fetch(`${MOCK_URL}/__control/state`)).json(),
  goal: (matchId: string, side: "home" | "away") =>
    fetch(`${MOCK_URL}/__control/matches/${matchId}/goal`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ side }),
    }),
  /**
   * Następne wywołania API (metoda + ścieżka, np. "/api/tournaments/:id/settings") dostają
   * podaną odpowiedź. Bez "times" działa do końca testu.
   */
  override: (o: { method: string; path: string; status?: number; body?: unknown; times?: number }) =>
    fetch(`${MOCK_URL}/__control/override`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(o),
    }),
  release: (matchId: string) =>
    fetch(`${MOCK_URL}/__control/matches/${matchId}/release`, { method: "POST" }),
}

export async function login(page: Page, email: string, password = PASSWORD) {
  await page.goto("/login")
  await page.getByLabel("Email").fill(email)
  await page.getByLabel("Hasło").fill(password)
  await page.getByRole("button", { name: "Zaloguj się" }).click()
}

/** Logowanie zakończone sukcesem — czeka, aż sesja jest zapisana i aplikacja opuściła /login. */
export async function loginAs(page: Page, email: string) {
  await login(page, email)
  await page.waitForURL((url) => !url.pathname.startsWith("/login"))
}

/** Nowy turniej w szkicu (organizator musi być zalogowany); zwraca jego id. */
export async function createTournament(page: Page, name = `Turniej ${Date.now() % 100_000}`) {
  await page.goto("/tournaments/new")
  await page.getByLabel("Nazwa").fill(name)
  await page.getByLabel("Rozpoczęcie", { exact: true }).fill("2026-11-14")
  await page.getByLabel("Rozpoczęcie - godzina").fill("09:00")
  await page.getByLabel("Zakończenie - godzina").fill("17:00")
  await page.getByRole("button", { name: "Utwórz turniej" }).click()
  await page.waitForURL(/\/tournaments\/[0-9a-f-]+\/setup/)
  return page.url().match(/tournaments\/([0-9a-f-]+)\//)![1]!
}

/** Każdy test startuje od czystego mocka (seed: organizator, sędzia, turniej z meczem LIVE 0:0). */
export const test = base.extend<{ resetMock: void }>({
  resetMock: [
    async ({}, use) => {
      await fetch(`${MOCK_URL}/__control/reset`, { method: "POST" })
      await use()
    },
    { auto: true },
  ],
})

export { expect }
