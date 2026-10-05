import { expect, MATCH_ID, mock, PUBLIC_SLUG, test } from "./fixtures"

test.describe("Portal publiczny → Wynik live", () => {
  test("kibic bez logowania widzi mecz LIVE i gol bez przeładowania", async ({ page }) => {
    await page.goto(`/t/${PUBLIC_SLUG}`)
    await expect(page.getByRole("heading", { level: 1, name: "Puchar E2E" })).toBeVisible()

    const match = page.getByRole("article", { name: "Orły E2E – Lwy E2E" }).first()
    await expect(match).toContainText("LIVE")
    const score = match.getByTestId("match-score")
    await expect(score).toHaveText(/Orły E2E\s*0\s*Lwy E2E\s*0/)

    // Portal dołączył do grupy turnieju w SignalR — dopiero wtedy "strzelamy".
    await expect.poll(async () => (await mock.state()).connections.tournaments).toBeGreaterThan(0)
    await mock.goal(MATCH_ID, "home")

    await expect(score).toHaveText(/Orły E2E\s*1\s*Lwy E2E\s*0/)
    // Bez przeładowania: to wciąż ta sama nawigacja.
    expect(await page.evaluate(() => performance.getEntriesByType("navigation").length)).toBe(1)
  })

  test("szczegóły meczu aktualizują wynik na żywo", async ({ page }) => {
    await page.goto(`/t/${PUBLIC_SLUG}/matches/${MATCH_ID}`)
    const score = page.getByTestId("detail-score")
    await expect(score).toHaveText("0:0")
    await expect.poll(async () => (await mock.state()).connections.tournaments).toBeGreaterThan(0)

    await mock.goal(MATCH_ID, "away")
    await mock.goal(MATCH_ID, "away")
    await expect(score).toHaveText("0:2")
  })

  test("nieistniejący turniej to 404", async ({ page }) => {
    const response = await page.goto("/t/nie-ma-takiego")
    expect(response?.status()).toBe(404)
    await expect(page.getByText("Nie ma takiej strony")).toBeVisible()
  })
})
