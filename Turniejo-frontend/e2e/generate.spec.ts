import { expect, loginAs, mock, ORGANIZER, test, TOURNAMENT_ID } from "./fixtures"

type Page = import("@playwright/test").Page

const CATEGORY = "0f000000-0000-4000-8000-000000000020"
const GROUP = "0f000000-0000-4000-8000-000000000030"
const GENERATE_URL = `/tournaments/${TOURNAMENT_ID}/schedule/generate`

const team = (i: number, fields: { groupId?: string | null; status?: string; name?: string } = {}) => ({
  id: `0f000000-0000-4000-8000-0000000002${String(i).padStart(2, "0")}`,
  tournamentId: TOURNAMENT_ID,
  teamId: `t${i}`,
  categoryId: CATEGORY,
  groupId: fields.groupId === undefined ? GROUP : fields.groupId,
  registrationStatus: fields.status ?? "Accepted",
  paymentStatus: "NotRequired",
  seedNumber: null,
  teamName: fields.name ?? `Drużyna ${i}`,
  teamShortName: null,
  teamLogoUrl: null,
})

/**
 * Turniej grupowy po zamknięciu zapisów, bez terminarza, trwający od jutra 9:00 (czasu polskiego)
 * przez dwa dni. Domyślnie 4 drużyny w Grupie A, czyli 6 meczów.
 */
async function readyToGenerate(teams = [1, 2, 3, 4].map((i) => team(i))) {
  const start = new Date(Date.now() + 86_400_000)
  start.setUTCHours(8, 0, 0, 0)
  await mock.tournament(TOURNAMENT_ID, {
    status: "RegistrationClosed",
    startAt: start.toISOString(),
    endAt: new Date(start.getTime() + 2 * 86_400_000).toISOString(),
  })
  await mock.override({ method: "GET", path: "/api/tournaments/:id/schedule", body: [] })
  await mock.override({ method: "GET", path: "/api/tournaments/:id/members", body: [] })
  await mock.override({ method: "GET", path: "/api/tournaments/:id/teams", body: teams })
}

const condition = (page: Page, text: string | RegExp) =>
  page.getByRole("listitem").filter({ hasText: text })
const generate = (page: Page) => page.getByRole("button", { name: "Generuj terminarz" })
const days = (page: Page) => page.getByRole("group", { name: "Dni gry" }).getByRole("listitem")

test.describe("Generator terminarza", () => {
  test.beforeEach(async ({ page }) => {
    await loginAs(page, ORGANIZER)
  })

  test("[PUC-063 PUC-066] plan dni: za dużo meczów, potem równy podział", async ({ page }) => {
    await readyToGenerate()
    await page.goto(GENERATE_URL)
    await page.getByRole("button", { name: "Dodaj dzień" }).click()
    await expect(days(page)).toHaveCount(2)

    await days(page).nth(0).getByLabel("Meczów").fill("6")
    await expect(page.getByText(/Dni mają o \d+ mecz/)).toBeVisible()
    await expect(generate(page)).toBeDisabled()

    await days(page).nth(0).getByLabel("Meczów").fill("60")
    await expect(days(page).nth(0)).toContainText("Tego dnia zmieści się najwyżej")

    await page.getByRole("button", { name: "Rozłóż mecze równo" }).click()
    await expect(days(page).nth(0).getByLabel("Meczów")).toHaveValue("3")
    await expect(days(page).nth(1).getByLabel("Meczów")).toHaveValue("3")
    await expect(page.getByText(/ma(ją)? swój dzień/)).toBeVisible()
    await expect(generate(page)).toBeEnabled()
  })

  test("[PUC-065] ten sam dzień dwa razy", async ({ page }) => {
    await readyToGenerate()
    await page.goto(GENERATE_URL)
    await page.getByRole("button", { name: "Dodaj dzień" }).click()
    const first = await days(page).nth(0).getByRole("textbox").first().inputValue()

    await days(page).nth(1).getByRole("textbox").first().fill(first)

    await expect(days(page).nth(1)).toContainText("Ten dzień jest już na liście")
    await expect(generate(page)).toBeDisabled()
  })

  test("[PUC-064 PUC-026] dzień spoza dat turnieju i mecz przed jego startem", async ({ page }) => {
    await readyToGenerate()
    await page.goto(GENERATE_URL)

    await days(page).nth(0).getByLabel("Pierwszy mecz").fill("08:00")
    await expect(days(page).nth(0)).toContainText("Turniej zaczyna się o 09:00")

    await days(page).nth(0).getByLabel("Pierwszy mecz").fill("09:00")
    await days(page).nth(0).getByRole("textbox").first().fill("2027-01-15")
    await expect(days(page).nth(0)).toContainText("Turniej trwa")
    await expect(generate(page)).toBeDisabled()
  })

  test("[PUC-025] za krótkie okno: komunikat z backendu po polsku", async ({ page }) => {
    await readyToGenerate()
    await mock.override({
      method: "POST",
      path: "/api/tournaments/:id/schedule/generate",
      status: 400,
      body: {
        code: "Validation.Schedule",
        message: "Not enough time available to schedule all matches — 2 match(es) could not be placed",
        type: 2,
      },
    })
    await page.goto(GENERATE_URL)

    await generate(page).click()
    await page.getByRole("alertdialog").getByRole("button", { name: "Generuj terminarz" }).click()

    await expect(page.getByText(/Za mało czasu - nie wszystkie mecze mieszczą się/).first()).toBeVisible()
    await expect(page).toHaveURL(new RegExp(`${GENERATE_URL}$`))
  })

  test("[PUC-023] wygenerowany terminarz prowadzi do listy meczów", async ({ page }) => {
    await readyToGenerate()
    await mock.override({ method: "POST", path: "/api/tournaments/:id/schedule/generate", body: { matchesCreated: 6 } })
    await page.goto(GENERATE_URL)

    await generate(page).click()
    await page.getByRole("alertdialog").getByRole("button", { name: "Generuj terminarz" }).click()

    await expect(page.getByText("Wygenerowano 6 meczów")).toBeVisible()
    await expect(page).toHaveURL(new RegExp(`/tournaments/${TOURNAMENT_ID}/schedule$`))
  })
})

test.describe("Generator terminarza: warunki", () => {
  test.beforeEach(async ({ page }) => {
    await loginAs(page, ORGANIZER)
  })

  test("[GRP-023 GRP-024 PUC-069] drużyna bez grupy blokuje, przypisanie jednym kliknięciem, oczekujące ostrzegają", async ({ page }) => {
    await readyToGenerate([team(1), team(2), team(3, { groupId: null, name: "Bez Grupy" }), team(4, { status: "Pending", groupId: null, name: "Czekający" })])
    await mock.override({ method: "PATCH", path: "/api/tournaments/:id/tournament-teams/:ttid/group", status: 204 })
    await page.goto(GENERATE_URL)

    await expect(condition(page, "nie ma grupy: Bez Grupy").getByLabel("Niespełnione")).toBeVisible()
    await expect(condition(page, "czeka na akceptację: Czekający").getByLabel("Uwaga")).toBeVisible()
    await expect(generate(page)).toBeDisabled()

    await page.getByRole("button", { name: "Przypisz 1 drużynę do „Grupa A”" }).click()
    await expect(page.getByText("Przypisano 1 drużynę")).toBeVisible()
  })

  test("[GRP-006] bez grup nie ma terminarza", async ({ page }) => {
    await readyToGenerate()
    await mock.override({ method: "GET", path: "/api/tournaments/:id/groups", body: [] })
    await page.goto(GENERATE_URL)

    await expect(condition(page, /grupa z 2 zaakceptowanymi|grup/).getByLabel("Niespełnione").first()).toBeVisible()
    await expect(generate(page)).toBeDisabled()
  })

  test("[PUC-027] bez aktywnego boiska nie ma terminarza", async ({ page }) => {
    await readyToGenerate()
    await mock.override({
      method: "GET",
      path: "/api/tournaments/:id/pitches",
      body: [{ id: "p1", tournamentId: TOURNAMENT_ID, name: "Boisko Główne", description: null, isActive: false }],
    })
    await page.goto(GENERATE_URL)

    await expect(condition(page, "Co najmniej jedno aktywne boisko").getByLabel("Niespełnione")).toBeVisible()
    await expect(generate(page)).toBeDisabled()
  })
})
