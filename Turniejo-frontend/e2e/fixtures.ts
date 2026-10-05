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
