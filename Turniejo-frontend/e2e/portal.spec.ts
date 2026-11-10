import { expect, MOCK_URL, mock, PUBLIC_SLUG, test } from "./fixtures"

type Settings = Record<
  | "showTeams"
  | "showPlayers"
  | "showSchedule"
  | "showStandings"
  | "showBracket"
  | "showLiveMatches"
  | "showStats",
  boolean
>

/** Portal z podmienionymi ustawieniami i formatem (reszta danych z seeda mocka). */
async function portalWith(settings: Partial<Settings>, format?: string) {
  const current = await (await fetch(`${MOCK_URL}/public/tournaments/${PUBLIC_SLUG}`)).json()
  await mock.override({
    method: "GET",
    path: `/public/tournaments/${PUBLIC_SLUG}`,
    body: {
      ...current,
      format: format ?? current.format,
      settings: { ...current.settings, ...settings },
    },
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
    test(`[${s.id}] wyłączona sekcja „${s.label}” znika z menu i jej adres daje 404`, async ({
      page,
    }) => {
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
  test("[PUB-016] strona główna pokazuje gościowi turnieje publiczne, a logowanie jest o jedno kliknięcie", async ({
    page,
  }) => {
    await page.goto("/")
    await expect(page).toHaveURL(/\/$/)
    await expect(page.getByRole("article", { name: "Puchar E2E" })).toBeVisible()
    await page.getByRole("banner").getByRole("link", { name: "Zaloguj się" }).click()
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

test("[PUB-006] tabela grupy i puchar bez drabinki nie dają błędu", async ({ page }) => {
  await portalWith({ showStandings: true })
  const standings = await page.goto(`/t/${PUBLIC_SLUG}/standings`)
  expect(standings?.status()).toBe(200)
  await expect(page.getByText(/coś poszło nie tak|błąd/i)).toHaveCount(0)

  await portalWith({ showBracket: true }, "Knockout")
  const bracket = await page.goto(`/t/${PUBLIC_SLUG}/bracket`)
  expect(bracket?.status()).toBe(200)
  await expect(page.getByText(/coś poszło nie tak|błąd/i)).toHaveCount(0)
})

test("[PUB-025] filtry meczów: boisko i dzień zawężają terminarz, a filtr trafia do adresu", async ({
  page,
}) => {
  const tournament = await (await fetch(`${MOCK_URL}/public/tournaments/${PUBLIC_SLUG}`)).json()
  const [base] = await (
    await fetch(`${MOCK_URL}/public/tournaments/${PUBLIC_SLUG}/schedule`)
  ).json()
  const pitch2 = "0f000000-0000-4000-8000-0000000000aa"
  const day = (offset: number) => new Date(Date.now() + offset * 86_400_000).toISOString()
  await mock.override({
    method: "GET",
    path: `/public/tournaments/${PUBLIC_SLUG}`,
    body: { ...tournament, pitches: [...tournament.pitches, { id: pitch2, name: "Boisko 2" }] },
  })
  await mock.override({
    method: "GET",
    path: `/public/tournaments/${PUBLIC_SLUG}/schedule`,
    body: [
      { ...base, id: "m1", status: "Scheduled", scheduledAt: day(1) },
      { ...base, id: "m2", status: "Scheduled", scheduledAt: day(1), pitchId: pitch2 },
      { ...base, id: "m3", status: "Scheduled", scheduledAt: day(2), pitchId: pitch2 },
    ],
  })

  await page.goto(`/t/${PUBLIC_SLUG}/schedule`)
  const filters = page.getByRole("search", { name: "Filtry meczów" })
  const choose = async (label: string, option: RegExp | string) => {
    await filters.getByRole("combobox", { name: label }).click()
    await page.getByRole("option", { name: option }).click()
  }
  const cards = page.getByRole("main").getByRole("article")
  await expect(cards).toHaveCount(3)

  await choose("Boisko", "Boisko 2")
  await expect(cards).toHaveCount(2)
  await expect(page.getByRole("status").filter({ hasText: "Znaleziono" })).toHaveText(
    "Znaleziono 2 mecze",
  )
  await expect(page).toHaveURL(/pitch=/)

  const dayName = new Intl.DateTimeFormat("pl-PL", {
    weekday: "long",
    day: "numeric",
    month: "long",
    timeZone: "Europe/Warsaw",
  }).format(new Date(day(2)))
  await choose("Dzień", new RegExp(`^${dayName}$`, "i"))
  await expect(cards).toHaveCount(1)

  await page.reload()
  await expect(cards).toHaveCount(1)
  await page.getByRole("button", { name: "Wyczyść wszystko" }).click()
  await expect(cards).toHaveCount(3)
})
