import { expect, loginAs, mock, ORGANIZER, PUBLIC_SLUG, test, TOURNAMENT_ID } from "./fixtures"
import { IDS } from "./mock/db"

// Mały herb w data URL: przeglądarka wczytuje go bez sieci (TeamLogo nie używa optymalizatora).
const LOGO =
  "data:image/svg+xml;utf8," +
  encodeURIComponent(
    '<svg xmlns="http://www.w3.org/2000/svg" width="8" height="8"><rect width="8" height="8" fill="red"/></svg>',
  )

test.describe("Herby drużyn", () => {
  test.beforeEach(async () => {
    await mock.teamLogo(IDS.homeTeam, LOGO)
  })

  test("panel: herb w terminarzu i tabeli, bez herbu inicjały", async ({ page }) => {
    await loginAs(page, ORGANIZER)

    await page.goto(`/tournaments/${TOURNAMENT_ID}/schedule`)
    await expect(page.getByRole("img", { name: "Herb: Orły E2E" }).first()).toBeVisible()
    // Lwy nie mają herbu: zamiast obrazka są inicjały.
    await expect(page.getByRole("img", { name: "Herb: Lwy E2E" })).toHaveCount(0)

    await page.goto(`/tournaments/${TOURNAMENT_ID}/standings`)
    await expect(
      page.getByRole("region", { name: /Tabela/ }).getByRole("img", { name: "Herb: Orły E2E" }),
    ).toBeVisible()
  })

  test("portal: herb w meczach i tabeli przy „Pokazuj drużyny”", async ({ page }) => {
    await page.goto(`/t/${PUBLIC_SLUG}/schedule`)
    await expect(page.getByRole("img", { name: "Herb: Orły E2E" }).first()).toBeVisible()

    await page.goto(`/t/${PUBLIC_SLUG}/standings`)
    await expect(page.getByRole("img", { name: "Herb: Orły E2E" }).first()).toBeVisible()
  })
})
