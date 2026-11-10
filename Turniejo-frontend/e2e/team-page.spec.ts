import {
  expect,
  loginAs,
  MATCH_ID,
  mock,
  ORGANIZER,
  PUBLIC_SLUG,
  test,
  TOURNAMENT_ID,
} from "./fixtures"

type Page = import("@playwright/test").Page

const ORLY = "0f000000-0000-4000-8000-000000000050"

const noHorizontalScroll = async (page: Page) =>
  expect
    .poll(() => page.evaluate(() => document.documentElement.scrollWidth - window.innerWidth))
    .toBeLessThanOrEqual(0)

test.describe("Strona drużyny w portalu", () => {
  test("[DRU-040] z tabeli na stronę drużyny: bilans, forma, wyniki i skład", async ({ page }) => {
    await mock.matchStatus(MATCH_ID, "Finished")
    await page.goto(`/t/${PUBLIC_SLUG}/standings`)
    await page.getByRole("link", { name: "Orły E2E" }).click()

    await expect(page).toHaveURL(new RegExp(`/t/${PUBLIC_SLUG}/teams/${ORLY}$`))
    await expect(page.getByRole("heading", { level: 1, name: "Orły E2E" })).toBeVisible()
    await expect(page).toHaveTitle("Orły E2E · Puchar E2E")

    const stats = page.getByRole("region", { name: "Bilans drużyny" })
    await expect(stats).toContainText("Grupa A")
    await expect(stats).toContainText("0-1-0")
    await expect(page.getByLabel("Forma").getByLabel("Remis")).toBeVisible()

    const results = page.getByRole("heading", { name: "Wyniki" }).locator("..")
    await expect(results.getByRole("article", { name: "Orły E2E – Lwy E2E" })).toBeVisible()

    // Skład: numer i nazwisko skrócone jak w reszcie portalu.
    const goalkeeper = page.getByTestId("squad-player").filter({ hasText: "Adam B." })
    await expect(goalkeeper).toContainText("1")
    await expect(goalkeeper).toContainText("Bramkarz")
    await expect(page.getByText("Bramka", { exact: true })).toHaveCount(0)

    await noHorizontalScroll(page)
  })

  test("[DRU-041] lista drużyn i strona meczu prowadzą do strony drużyny", async ({ page }) => {
    await page.goto(`/t/${PUBLIC_SLUG}/teams`)
    await page.getByRole("link", { name: "Lwy E2E: mecze, wyniki i skład" }).click()
    await expect(page.getByRole("heading", { level: 1, name: "Lwy E2E" })).toBeVisible()
    // Trwający mecz ma własną sekcję na stronie drużyny.
    await expect(page.getByRole("heading", { name: "Na żywo" })).toBeVisible()

    await page.goto(`/t/${PUBLIC_SLUG}/matches/${MATCH_ID}`)
    await page.getByRole("link", { name: "Orły E2E" }).click()
    await expect(page.getByRole("heading", { level: 1, name: "Orły E2E" })).toBeVisible()
  })

  test("[DRU-042] bez „Pokazuj zawodników” strona drużyny nie ma składu", async ({ page }) => {
    await mock.override({
      method: "GET",
      path: `/public/tournaments/${PUBLIC_SLUG}`,
      body: {
        id: TOURNAMENT_ID,
        name: "Puchar E2E",
        slug: PUBLIC_SLUG,
        status: "InProgress",
        format: "Group",
        startAt: "2026-11-10T08:00:00Z",
        endAt: "2026-11-10T16:00:00Z",
        description: null,
        settings: {
          showTeams: true,
          showPlayers: false,
          showSchedule: true,
          showStandings: true,
          showBracket: false,
          showLiveMatches: true,
          showStats: true,
          showInDirectory: true,
        },
        categories: [{ id: "0f000000-0000-4000-8000-000000000020", name: "U12", order: 1 }],
        groups: [],
        pitches: [],
      },
    })
    await page.goto(`/t/${PUBLIC_SLUG}/teams/${ORLY}`)

    await expect(page.getByRole("heading", { level: 1, name: "Orły E2E" })).toBeVisible()
    await expect(page.getByRole("heading", { name: "Skład" })).toHaveCount(0)
    expect(await mock.calls("GET", "/players")).toBe(0)
  })
})

test("[DRU-043] panel: z tabeli na stronę drużyny z meczami i wynikami", async ({ page }) => {
  await mock.matchStatus(MATCH_ID, "Finished")
  await loginAs(page, ORGANIZER)
  await page.goto(`/tournaments/${TOURNAMENT_ID}/standings`)
  await page.getByRole("link", { name: "Lwy E2E" }).click()

  await expect(page).toHaveURL(/\/tournaments\/.+\/teams\/.+051$/)
  await expect(page.getByText("Mecze i wyniki")).toBeVisible()
  const row = page.getByTestId("team-match")
  await expect(row).toHaveCount(1)
  await expect(row).toContainText("vs Orły E2E")
  await expect(row).toContainText("gość")
  await expect(row.getByLabel("Remis")).toBeVisible()

  // Przeciwnik prowadzi do swojej strony drużyny.
  await row.getByRole("link", { name: "Orły E2E" }).click()
  await expect(page.getByRole("heading", { name: "Orły E2E" })).toBeVisible()
  await noHorizontalScroll(page)
})
