import { expect, loginAs, mock, ORGANIZER, test } from "./fixtures"

test.describe("Tworzenie turnieju → Zmiana statusu", () => {
  test("organizator tworzy turniej i otwiera zapisy", async ({ page }) => {
    const name = `Turniej Jesienny ${Date.now() % 100_000}`
    await loginAs(page, ORGANIZER)
    await expect(page).toHaveURL(/\/tournaments$/)

    await page.goto("/tournaments/new")
    await page.getByLabel("Nazwa").fill(name)
    await page.getByLabel("Rozpoczęcie", { exact: true }).fill("2026-11-14")
    await page.getByLabel("Rozpoczęcie - godzina").fill("09:00")
    // Data zakończenia podąża za rozpoczęciem.
    await expect(page.getByLabel("Zakończenie", { exact: true })).toHaveValue("2026-11-14")
    await page.getByLabel("Zakończenie - godzina").fill("17:00")
    await page.getByRole("button", { name: "Utwórz turniej" }).click()

    // Po utworzeniu turnieju startuje kreator konfiguracji — każdy krok można pominąć.
    await expect(page).toHaveURL(/\/tournaments\/[0-9a-f-]+\/setup(\?step=\w+)?$/)
    await page.getByRole("link", { name: "Pomiń konfigurację" }).click()
    await expect(page).toHaveURL(/\/tournaments\/[0-9a-f-]+\/dashboard$/)
    const steps = page.getByRole("list", { name: "Etapy turnieju" })
    await expect(steps.locator("[aria-current=step]")).toHaveText("Szkic")

    // Przejście wymaga potwierdzenia w dialogu.
    await page.getByRole("button", { name: "Otwórz zapisy" }).click()
    const dialog = page.getByRole("alertdialog")
    await expect(dialog).toContainText("Status turnieju zmieni się na „Zapisy otwarte”")
    await dialog.getByRole("button", { name: "Otwórz zapisy" }).click()

    await expect(steps.locator("[aria-current=step]")).toHaveText("Zapisy otwarte")
    await expect(page.getByRole("button", { name: "Zamknij zapisy" })).toBeVisible()

    const created = (await mock.state()).tournaments.find((t) => t.name === name)
    expect(created?.status).toBe("RegistrationOpen")

    // Nowy turniej jest na liście organizatora.
    await page.goto("/tournaments")
    await expect(page.getByRole("link", { name: new RegExp(name) })).toBeVisible()
  })

  test("data końca przed startem nie przechodzi walidacji", async ({ page }) => {
    await loginAs(page, ORGANIZER)
    await expect(page).toHaveURL(/\/tournaments$/)
    await page.goto("/tournaments/new")
    await page.getByLabel("Nazwa").fill("Zły termin")
    await page.getByLabel("Rozpoczęcie", { exact: true }).fill("2026-11-14")
    await page.getByLabel("Rozpoczęcie - godzina").fill("17:00")
    await page.getByLabel("Zakończenie - godzina").fill("09:00")
    await page.getByRole("button", { name: "Utwórz turniej" }).click()
    await expect(page.locator("[data-slot=field-error]").first()).toBeVisible()
    await expect(page).toHaveURL(/\/tournaments\/new$/)
    expect((await mock.state()).tournaments.map((t) => t.name)).not.toContain("Zły termin")
  })
})
