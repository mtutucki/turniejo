import { expect, loginAs, MATCH_ID, mock, REFEREE, test } from "./fixtures"

const timer = (page: import("@playwright/test").Page) => page.getByRole("timer")

test.describe("Mecz na żywo: sterowanie", () => {
  test.beforeEach(async ({ page }) => {
    await mock.matchStatus(MATCH_ID, "Scheduled")
    await loginAs(page, REFEREE)
    await page.goto(`/matches/${MATCH_ID}/live`)
    await expect(page.getByRole("button", { name: "START" })).toBeEnabled()
  })

  test("[MNZ-002 MNZ-003] start uruchamia zegar, pauza go zatrzymuje", async ({ page }) => {
    await expect(timer(page)).toHaveAccessibleName("Czas meczu 00:00")

    await page.getByRole("button", { name: "START" }).click()
    await expect(page.getByRole("button", { name: "PAUZA" })).toBeVisible()
    await expect(timer(page)).not.toHaveAccessibleName("Czas meczu 00:00", { timeout: 5_000 })

    await page.getByRole("button", { name: "PAUZA" }).click()
    await expect(page.getByRole("button", { name: "WZNÓW" })).toBeVisible()
    const paused = await timer(page).getAttribute("aria-label")
    await page.waitForTimeout(2_000)
    await expect(timer(page)).toHaveAttribute("aria-label", paused!)
  })

  test("[MNZ-005] podwójne kliknięcie START i KONIEC wysyła jedno żądanie", async ({ page }) => {
    await page.getByRole("button", { name: "START" }).dblclick()
    await expect(page.getByRole("button", { name: "PAUZA" })).toBeVisible()
    expect(await mock.calls("POST", "/start")).toBe(1)

    await page.getByRole("button", { name: "KONIEC" }).click()
    const dialog = page.getByRole("dialog")
    await dialog.getByRole("button", { name: "Zakończ mecz" }).dblclick()
    await expect(dialog).toBeHidden()
    expect(await mock.calls("POST", "/finish")).toBe(1)
    await expect(page.getByRole("button", { name: "PAUZA" })).toBeHidden()
  })
})

test.describe("Koniec meczu: walidacja rozstrzygnięcia", () => {
  test.beforeEach(async ({ page }) => {
    await loginAs(page, REFEREE)
    await page.goto(`/matches/${MATCH_ID}/live`)
    await page.getByRole("button", { name: "KONIEC" }).click()
  })

  const dialog = (page: import("@playwright/test").Page) => page.getByRole("dialog", { name: "Zakończyć mecz?" })

  test("[PUC-040b] puste pola karnych", async ({ page }) => {
    await dialog(page).getByRole("button", { name: "Karne" }).click()
    await dialog(page).getByRole("button", { name: "Zakończ mecz" }).click()
    await expect(dialog(page).getByText("Wpisz wynik rzutów karnych")).toBeVisible()
    expect(await mock.calls("POST", "/finish")).toBe(0)
  })

  test("[PUC-040a] karne zakończone remisem", async ({ page }) => {
    await dialog(page).getByRole("button", { name: "Karne" }).click()
    await dialog(page).getByLabel("Karne: Orły E2E").fill("4")
    await dialog(page).getByLabel("Karne: Lwy E2E").fill("4")
    await dialog(page).getByRole("button", { name: "Zakończ mecz" }).click()
    await expect(dialog(page).getByText("Karne muszą wyłonić zwycięzcę")).toBeVisible()
    expect(await mock.calls("POST", "/finish")).toBe(0)
  })

  test("[PUC-043] walkower bez wskazania zwycięzcy", async ({ page }) => {
    await dialog(page).getByRole("button", { name: "Walkower" }).click()
    await dialog(page).getByRole("button", { name: "Zakończ mecz" }).click()
    await expect(dialog(page).getByText("Wybierz drużynę, która wygrywa walkowerem")).toBeVisible()

    await dialog(page).getByRole("button", { name: "LWY" }).click()
    await dialog(page).getByRole("button", { name: "Zakończ mecz" }).click()
    await expect(dialog(page)).toBeHidden()
    expect(await mock.calls("POST", "/finish")).toBe(1)
  })
})
