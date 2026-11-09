import { expect, MOCK_URL, mock, PUBLIC_SLUG, test } from "./fixtures"

type Settings = Record<"showTeams" | "showPlayers" | "showSchedule" | "showStandings" | "showBracket" | "showLiveMatches" | "showStats", boolean>

/** Portal z podmienionymi ustawieniami i formatem (reszta danych z seeda mocka). */
async function portalWith(settings: Partial<Settings>, format?: string) {
  const current = await (await fetch(`${MOCK_URL}/public/tournaments/${PUBLIC_SLUG}`)).json()
  await mock.override({
    method: "GET",
    path: `/public/tournaments/${PUBLIC_SLUG}`,
    body: { ...current, format: format ?? current.format, settings: { ...current.settings, ...settings } },
  })
}

const tab = (page: import("@playwright/test").Page, name: string) =>
  page.getByRole("banner").getByRole("link", { name, exact: true })

test.describe("Portal: sekcje wg ustawień organizatora", () => {
  const sections = [
    { id: "PUB-003a", key: "showTeams", label: "Drużyny", path: "teams" },
    { id: "PUB-003c", key: "showSchedule", label: "Terminarz", path: "schedule" },
    { id: "PUB-003d", key: "showStandings", label: "Tabele", path: "standings" },
    { id: "PUB-003g", key: "showStats", label: "Statystyki", path: "stats" },
  ] as const

  for (const s of sections) {
    test(`[${s.id}] wyłączona sekcja „${s.label}” znika z menu i jej adres daje 404`, async ({ page }) => {
      await portalWith({ [s.key]: true })
      await page.goto(`/t/${PUBLIC_SLUG}`)
      await expect(tab(page, s.label)).toBeVisible()

      await portalWith({ [s.key]: false })
      await page.goto(`/t/${PUBLIC_SLUG}`)
      await expect(tab(page, s.label)).toHaveCount(0)
      const response = await page.goto(`/t/${PUBLIC_SLUG}/${s.path}`)
      expect(response?.status()).toBe(404)
    })
  }

  test("[PUB-003e] drabinka jest tylko w pucharze i tylko gdy włączona", async ({ page }) => {
    await portalWith({ showBracket: true }, "Group")
    await page.goto(`/t/${PUBLIC_SLUG}`)
    await expect(tab(page, "Drabinka")).toHaveCount(0)

    await portalWith({ showBracket: true }, "Knockout")
    await page.goto(`/t/${PUBLIC_SLUG}`)
    await expect(tab(page, "Drabinka")).toBeVisible()
    await expect(tab(page, "Tabele")).toHaveCount(0)

    await portalWith({ showBracket: false }, "Knockout")
    await page.goto(`/t/${PUBLIC_SLUG}`)
    await expect(tab(page, "Drabinka")).toHaveCount(0)
  })

  test("[GPK-012] format mieszany ma i tabele, i drabinkę", async ({ page }) => {
    await portalWith({ showBracket: true, showStandings: true }, "GroupAndKnockout")
    await page.goto(`/t/${PUBLIC_SLUG}`)
    await expect(tab(page, "Tabele")).toBeVisible()
    await expect(tab(page, "Drabinka")).toBeVisible()
  })
})

test.describe("Strony dla gościa", () => {
  test("[PUB-016] strona główna prowadzi do logowania bez callbackUrl", async ({ page }) => {
    await page.goto("/")
    await expect(page).toHaveURL(/\/login$/)
  })

  for (const [path, heading] of [
    ["/terms", /Regulamin/],
    ["/privacy", /Polityka prywatności/],
    ["/help", /Pomoc/],
  ] as const) {
    test(`[PUB-014] strona stała ${path} działa bez logowania`, async ({ page }) => {
      const response = await page.goto(path)
      expect(response?.status()).toBe(200)
      await expect(page.getByRole("heading", { level: 1, name: heading })).toBeVisible()
    })
  }
})
