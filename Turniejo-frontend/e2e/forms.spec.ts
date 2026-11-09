import { createTournament, expect, loginAs, mock, ORGANIZER, test } from "./fixtures"

test.describe("Dane turnieju: walidacja", () => {
  test.beforeEach(async ({ page }) => {
    await loginAs(page, ORGANIZER)
    await page.goto("/tournaments/new")
  })

  test("[UST-001a] pusta nazwa", async ({ page }) => {
    await page.getByRole("button", { name: "Utwórz turniej" }).click()
    await expect(page.getByText("Podaj nazwę turnieju")).toBeVisible()
    await expect(page).toHaveURL(/\/tournaments\/new$/)
  })

  for (const limit of ["-1", "0"]) {
    test(`[UST-001c UST-019a DRU-006] limit drużyn ${limit} jest odrzucony`, async ({ page }) => {
      await page.getByLabel("Nazwa").fill("Zły limit")
      await page.getByLabel("Maksymalna liczba drużyn").fill(limit)
      await page.getByRole("button", { name: "Utwórz turniej" }).click()
      await expect(page.getByText("Limit musi wynosić co najmniej 1")).toBeVisible()
      expect((await mock.state()).tournaments.map((t) => t.name)).not.toContain("Zły limit")
    })
  }

  test("[UST-019b] limit drużyn 1 przechodzi", async ({ page }) => {
    await page.getByLabel("Nazwa").fill("Jedna drużyna")
    await page.getByLabel("Rozpoczęcie", { exact: true }).fill("2026-11-14")
    await page.getByLabel("Rozpoczęcie - godzina").fill("09:00")
    await page.getByLabel("Zakończenie - godzina").fill("17:00")
    await page.getByLabel("Maksymalna liczba drużyn").fill("1")
    await page.getByRole("button", { name: "Utwórz turniej" }).click()
    await expect(page).toHaveURL(/\/setup/)
    expect((await mock.state()).tournaments.map((t) => t.name)).toContain("Jedna drużyna")
  })
})

test.describe("Zasady gry: walidacja", () => {
  test.beforeEach(async ({ page }) => {
    await loginAs(page, ORGANIZER)
    const id = await createTournament(page)
    await page.goto(`/tournaments/${id}/settings`)
    await page.getByRole("button", { name: "Zasady gry" }).click()
    await expect(page.getByLabel("Wygrana")).toBeVisible()
    await expect(page.getByLabel("Wygrana")).toHaveValue("3")
  })

  const save = (page: import("@playwright/test").Page) =>
    page.getByRole("button", { name: "Zapisz zasady gry" }).click()

  test("[UST-006a] wygrana za mniej punktów niż remis", async ({ page }) => {
    await page.getByLabel("Wygrana").fill("1")
    await page.getByLabel("Remis").fill("2")
    await save(page)
    await expect(page.getByText("Wygrana nie może dawać mniej punktów niż remis")).toBeVisible()
  })

  test("[UST-006b] remis za mniej punktów niż porażka", async ({ page }) => {
    await page.getByLabel("Remis").fill("0")
    await page.getByLabel("Porażka").fill("1")
    await save(page)
    await expect(page.getByText("Remis nie może dawać mniej punktów niż porażka")).toBeVisible()
  })

  test("[UST-006c] walkower 2:2", async ({ page }) => {
    await page.getByLabel("Gole gospodarzy").fill("2")
    await page.getByLabel("Gole gości").fill("2")
    await save(page)
    await expect(page.getByText("Walkower nie może kończyć się remisem")).toBeVisible()
  })

  test("[UST-006d] mecz trwający 0 minut", async ({ page }) => {
    await page.getByLabel("Długość meczu").fill("0")
    await save(page)
    await expect(page.getByText("Mecz musi trwać co najmniej minutę")).toBeVisible()
  })

  test("[UST-006e] wartości ujemne i ułamkowe", async ({ page }) => {
    await page.getByLabel("Przerwa między meczami").fill("-5")
    await page.getByLabel("Porażka").fill("0.5")
    await save(page)
    await expect(page.getByText("Nie może być ujemne")).toBeVisible()
    await expect(page.getByText("Podaj liczbę całkowitą")).toBeVisible()
  })
})
