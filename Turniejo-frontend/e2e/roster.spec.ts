import { expect, loginAs, ORGANIZER, test } from "./fixtures"

const ORGANIZATION_ID = "0f000000-0000-4000-8000-000000000001"

test.describe("Kadra drużyny", () => {
  test.beforeEach(async ({ page }) => {
    await loginAs(page, ORGANIZER)
    await page.goto(`/organizations/${ORGANIZATION_ID}/teams`)
    await page.getByRole("link", { name: "Kadra" }).first().click()
    await expect(page.getByRole("heading", { name: "Orły E2E" })).toBeVisible()
  })

  test("[DRU-024 DRU-026] organizator dodaje zawodnika, a zajęty numer blokuje zapis", async ({ page }) => {
    await expect(page.getByText("2 zawodników w kadrze")).toBeVisible()

    await page.getByRole("button", { name: "Dodaj zawodnika" }).click()
    const dialog = page.getByRole("dialog")
    await dialog.getByLabel("Imię").fill("Damian")
    await dialog.getByLabel("Nazwisko").fill("Nowy")
    await dialog.getByLabel("Data urodzenia").fill("2014-05-05")
    await dialog.getByLabel("Numer koszulki (opcjonalnie)").fill("4")
    await dialog.getByRole("button", { name: "Dodaj do kadry" }).click()
    await expect(dialog.getByText("Ten numer jest już zajęty w drużynie")).toBeVisible()

    await dialog.getByLabel("Numer koszulki (opcjonalnie)").fill("7")
    await dialog.getByRole("button", { name: "Dodaj do kadry" }).click()
    await expect(dialog).toBeHidden()
    await expect(page.getByText("Damian Nowy", { exact: true })).toBeVisible()
    await expect(page.getByText("3 zawodników w kadrze")).toBeVisible()
  })

  test("usunięcie zawodnika ze składu turnieju pyta o skład i zostawia go wśród byłych", async ({
    page,
  }) => {
    await expect(page.getByText("W składzie: Puchar E2E (trwa)")).toBeVisible()
    await page.getByRole("button", { name: "Usuń Adam Bramka z kadry" }).click()

    const dialog = page.getByRole("alertdialog")
    await expect(
      dialog.getByText("Jest w składzie: Puchar E2E (trwa). Co z tym składem?"),
    ).toBeVisible()
    await dialog.getByText("Zostaw w składzie").click()
    await dialog.getByRole("button", { name: "Usuń z kadry" }).click()
    await expect(dialog).toBeHidden()

    await page.getByRole("button", { name: "Byli zawodnicy (2)" }).click()
    await expect(page.getByText("Adam Bramka", { exact: true })).toBeVisible()
    await expect(page.getByText(/Wciąż w składzie: Puchar E2E \(trwa\)/)).toBeVisible()
    await expect(page.getByText("1 zawodnik w kadrze")).toBeVisible()
  })

  test("były zawodnik wraca do kadry", async ({ page }) => {
    await page.getByRole("button", { name: "Byli zawodnicy (1)" }).click()
    await expect(page.getByText(/2 turnieje w historii/)).toBeVisible()
    await page.getByRole("button", { name: "Przywróć" }).click()

    await expect(page.getByText("Cezary Dawny znów w kadrze")).toBeVisible()
    await expect(page.getByText("3 zawodników w kadrze")).toBeVisible()
    await expect(page.getByRole("button", { name: /Byli zawodnicy/ })).toBeHidden()
  })

  test("zawodnik, który nigdzie nie grał, znika bez pytania o skład", async ({ page }) => {
    await page.getByRole("button", { name: "Usuń Bartek Lewy z kadry" }).click()
    const dialog = page.getByRole("alertdialog")
    await expect(
      dialog.getByText("Zawodnik nie grał jeszcze w żadnym turnieju tej drużyny."),
    ).toBeVisible()
    await expect(dialog.getByText("Co z tym składem?")).toBeHidden()
    await dialog.getByRole("button", { name: "Usuń z kadry" }).click()

    await expect(page.getByText("Bartek Lewy", { exact: true })).toBeHidden()
    await expect(page.getByRole("button", { name: "Byli zawodnicy (1)" })).toBeVisible()
  })
})
