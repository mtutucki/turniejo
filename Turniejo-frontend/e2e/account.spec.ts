import { expect, login, loginAs, mock, PASSWORD, REFEREE, test } from "./fixtures"

// Sidebar z menu jest tylko na desktopie; na telefonie te same pozycje są w arkuszu "Więcej".
test.skip(({ isMobile }) => isMobile, "menu boczne tylko na desktopie")

test.describe("Moje konto", () => {
  test("sędzia bez organizacji zostaje organizatorem z menu", async ({ page }) => {
    await loginAs(page, REFEREE)
    const menuItem = page.getByRole("link", { name: "Zostań organizatorem" })
    await menuItem.click()

    // Formularz otwiera się dopiero po kliknięciu, nie od razu po wejściu na stronę.
    const dialog = page.getByRole("dialog")
    await expect(page.getByRole("heading", { name: "Zostań organizatorem" })).toBeVisible()
    await expect(dialog).toBeHidden()
    await page.getByRole("button", { name: "Załóż organizację" }).click()
    await expect(dialog.getByLabel("Nazwa organizacji")).toHaveValue("Sebastian Sędzia")
    await dialog.getByLabel("Nazwa organizacji").fill("KS Gwizdek")
    await dialog.getByRole("button", { name: "Utwórz organizację" }).click()

    await expect(page).toHaveURL(/\/tournaments$/)
    await expect(menuItem).toBeHidden()
    await expect.poll(async () => (await mock.state()).organizations).toContain("KS Gwizdek")
  })

  test("zmiana danych osobowych od razu widać w menu", async ({ page }) => {
    await loginAs(page, REFEREE)
    await page.goto("/account")

    // Najpierw sam podgląd danych, formularz dopiero po "Edytuj".
    await expect(page.getByText(REFEREE).first()).toBeVisible()
    await expect(page.getByLabel("Imię")).toBeHidden()
    await page.getByRole("button", { name: "Edytuj" }).click()

    await expect(page.getByLabel("E-mail")).toHaveValue(REFEREE)
    await expect(page.getByLabel("E-mail")).toBeDisabled()
    await page.getByLabel("Imię").fill("Szymon")
    await page.getByLabel("Telefon (opcjonalnie)").fill("500 100 200")
    await page.getByRole("button", { name: "Zapisz" }).click()

    await expect(page.getByText("Dane zapisane")).toBeVisible()
    await expect(page.getByLabel("Imię")).toBeHidden()
    await expect(page.getByText("500 100 200")).toBeVisible()
    await expect(page.getByText("Szymon Sędzia").first()).toBeVisible()
    await page.reload()
    await expect(page.getByText("500 100 200")).toBeVisible()
  })

  test("zmiana hasła wymaga obecnego hasła i działa przy kolejnym logowaniu", async ({
    page,
    browser,
  }) => {
    const newPassword = "NoweHaslo123"
    await loginAs(page, REFEREE)
    await page.goto("/account")

    await expect(page.getByLabel("Obecne hasło")).toBeHidden()
    await page.getByRole("button", { name: "Zmień hasło" }).click()
    await page.getByLabel("Obecne hasło").fill("ZleHaslo123")
    await page.getByLabel("Nowe hasło", { exact: true }).fill(newPassword)
    await page.getByLabel("Powtórz nowe hasło").fill(newPassword)
    await page.getByRole("button", { name: "Zapisz nowe hasło" }).click()
    await expect(page.getByText("Obecne hasło jest nieprawidłowe")).toBeVisible()

    await page.getByLabel("Obecne hasło").fill(PASSWORD)
    await page.getByRole("button", { name: "Zapisz nowe hasło" }).click()
    await expect(page.getByText(/Hasło zmienione/)).toBeVisible()
    await expect(page.getByLabel("Obecne hasło")).toBeHidden()
    // Po zmianie user zostaje zalogowany (frontend loguje go od razu nowym hasłem).
    await page.reload()
    await expect(page.getByRole("button", { name: "Zmień hasło" })).toBeVisible()

    // Inne urządzenie: stare hasło już nie działa, nowe tak.
    const other = await (await browser.newContext()).newPage()
    await login(other, REFEREE, PASSWORD)
    await expect(other.getByText(/nieprawidłow/i)).toBeVisible()
    await login(other, REFEREE, newPassword)
    await other.waitForURL((url) => !url.pathname.startsWith("/login"))
  })
})
