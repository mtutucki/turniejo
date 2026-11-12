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
  // Zdarzenia wysyłamy dopiero, gdy strona dołączy do grupy turnieju w SignalR. Wcześniej gol i koniec
  // meczu potrafiły przyjść przed połączeniem i tabela czekała na nie bez końca (test niestabilny).
  await expect.poll(async () => (await mock.state()).connections.tournaments).toBeGreaterThan(0)

  // Sędzia kończy mecz na swoim telefonie.
  await mock.goal(MATCH_ID, "home")
  await mock.matchStatus(MATCH_ID, "Finished")

  await expect(winner.getByRole("cell").last()).toHaveText("3")
  await expect(winner.getByRole("cell").first()).toHaveText("1")
})

test("[TAB-03] drużyna przeniesiona do innej grupy od razu zmienia tabele, bez odświeżania", async ({ page }) => {
  // Przed terminarzem: po jego wygenerowaniu grup nie da się już zmieniać (GRP-031).
  await mock.tournament(TOURNAMENT_ID, { status: "RegistrationClosed" })
  await loginAs(page, ORGANIZER)
  await page.goto(`/tournaments/${TOURNAMENT_ID}/standings`)
  await expect(table(page).getByRole("row", { name: /Lwy E2E/ })).toBeVisible()

  // Druga grupa i przeniesienie Lwów, przechodząc między zakładkami (bez przeładowania strony).
  await page.getByRole("link", { name: "Kategorie i grupy" }).first().click()
  await page.getByRole("button", { name: "Dodaj grupę" }).click()
  const dialog = page.getByRole("dialog", { name: "Nowa grupa · U12" })
  await dialog.getByRole("button", { name: "Zapisz" }).click()
  await expect(dialog).toBeHidden()

  await page.getByRole("link", { name: "Drużyny" }).first().click()
  await page.getByRole("combobox", { name: "Grupa: Lwy E2E" }).click()
  await page.getByRole("option", { name: "Grupa B" }).click()
  await expect(page.getByRole("combobox", { name: "Grupa: Lwy E2E" })).toHaveText("Grupa B")

  await page.getByRole("link", { name: "Tabela" }).first().click()
  await expect(table(page).getByRole("row", { name: /Orły E2E/ })).toBeVisible()
  await expect(table(page).getByRole("row", { name: /Lwy E2E/ })).toHaveCount(0)
  await expect(
    page.getByRole("region", { name: "Tabela: Grupa B" }).getByRole("row", { name: /Lwy E2E/ }),
  ).toBeVisible()
})
