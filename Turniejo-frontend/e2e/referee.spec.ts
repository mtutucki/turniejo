import { expect, loginAs, MATCH_ID, mock, REFEREE, test } from "./fixtures"

test.describe("Panel sędziego → Dodanie gola → Aktualizacja wyniku", () => {
  test("sędzia dodaje gola, wynik zapisuje się i zostaje po odświeżeniu", async ({ page }) => {
    await loginAs(page, REFEREE)
    await page.goto("/referee/matches")

    const card = page.getByRole("link", { name: /Orły E2E/ })
    await expect(card).toContainText("Lwy E2E")
    await card.click()
    await expect(page).toHaveURL(new RegExp(`/matches/${MATCH_ID}/live$`))

    // Wynik ma aria-label "Drużyna: N" (czytniki ekranu) — po nim szukamy konkretnej wartości.
    const score = (team: string, value: number) =>
      page.getByLabel(`${team}: ${value}`, { exact: true })
    await expect(score("Orły E2E", 0)).toHaveText("0")
    // Wskaźnik połączenia SignalR w nagłówku panelu.
    await expect(page.getByRole("status").filter({ hasText: "Na żywo" })).toBeVisible()

    await page.getByRole("button", { name: "Szybki gol: ORŁ" }).click()
    await expect(score("Orły E2E", 1)).toHaveText("1")
    await expect(score("Lwy E2E", 0)).toHaveText("0")
    await expect(page.getByRole("region", { name: "Zdarzenia" })).toContainText("Gol")

    await expect
      .poll(async () => (await mock.state()).matches[0])
      .toMatchObject({ homeScore: 1, awayScore: 0 })

    await page.reload()
    await expect(page.getByLabel("Orły E2E: 1", { exact: true })).toHaveText("1")
  })

  test("gol dodany z innego urządzenia pojawia się u sędziego przez SignalR", async ({ page }) => {
    await loginAs(page, REFEREE)
    await page.goto(`/matches/${MATCH_ID}/live`)
    await expect(page.getByLabel("Lwy E2E: 0", { exact: true })).toHaveText("0")
    await expect.poll(async () => (await mock.state()).connections.matches).toBeGreaterThan(0)

    await mock.goal(MATCH_ID, "away")
    await expect(page.getByLabel("Lwy E2E: 1", { exact: true })).toHaveText("1")
  })

  test("mecz oddany z innego urządzenia od razu wraca do wolnych", async ({ page }) => {
    await loginAs(page, REFEREE)
    await page.goto("/referee/matches")
    await expect(page.getByRole("link", { name: /Orły E2E/ })).toBeVisible()
    await expect.poll(async () => (await mock.state()).connections.tournaments).toBeGreaterThan(0)

    await mock.release(MATCH_ID)
    await expect(page.getByText("Trwają bez sędziego")).toBeVisible()
    await expect(page.getByRole("button", { name: /Sędziuję ten mecz: Orły E2E/ })).toBeVisible()
  })
})
