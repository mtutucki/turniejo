import { expect, loginAs, MATCH_ID, mock, ORGANIZER, test, TOURNAMENT_ID } from "./fixtures"

// Tabela grupy w panelu organizatora: drużyny widać od razu po ułożeniu grup, a wynik zakończonego
// meczu wchodzi do tabeli bez odświeżania strony.
const table = (page: import("@playwright/test").Page) =>
  page.getByRole("region", { name: "Tabela: Grupa A" })

test("[TAB-01] przed pierwszym zakończonym meczem tabela pokazuje drużyny grupy z zerami", async ({
  page,
}) => {
  await loginAs(page, ORGANIZER)
  await page.goto(`/tournaments/${TOURNAMENT_ID}/standings`)

  await expect(table(page).getByRole("row", { name: /Orły E2E/ })).toBeVisible()
  await expect(table(page).getByRole("row", { name: /Lwy E2E/ })).toBeVisible()
  await expect(table(page)).not.toContainText("Brak drużyn w grupie")
})

test("[TAB-02] wynik zakończonego meczu pojawia się w tabeli bez odświeżania", async ({ page }) => {
  await loginAs(page, ORGANIZER)
  await page.goto(`/tournaments/${TOURNAMENT_ID}/standings`)
  const winner = table(page).getByRole("row", { name: /Orły E2E/ })
  await expect(winner.getByRole("cell").last()).toHaveText("0")

  // Sędzia kończy mecz na swoim telefonie.
  await mock.goal(MATCH_ID, "home")
  await mock.matchStatus(MATCH_ID, "Finished")

  await expect(winner.getByRole("cell").last()).toHaveText("3")
  await expect(winner.getByRole("cell").first()).toHaveText("1")
})
