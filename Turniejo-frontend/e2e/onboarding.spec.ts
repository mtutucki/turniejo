import { expect, mock, PASSWORD, PUBLIC_SLUG, test, VIEWER_CODE } from "./fixtures"

test.describe("Zaproszenie → rejestracja rodzica", () => {
  test("[KON-014] link z kodem prowadzi przez rejestrację i od razu dołącza, bez roli organizatora", async ({
    page,
  }) => {
    const email = `rodzic.${Date.now()}@e2e.pl`

    // Link od organizatora, bez konta → logowanie → "Zarejestruj się" z zachowanym celem.
    await page.goto(`/join?code=${VIEWER_CODE}`)
    await expect(page).toHaveURL(/\/login\?/)
    await page.getByRole("link", { name: "Zarejestruj się" }).click()
    await expect(page).toHaveURL(/\/register\?callbackUrl=.*join/)
    await expect(page.getByText("Po założeniu konta od razu dołączysz do turnieju")).toBeVisible()
    await expect(page.getByRole("radio", { name: /Dołączam do turnieju/ })).toBeChecked()

    await page.getByLabel("Imię").fill("Anna")
    await page.getByLabel("Nazwisko").fill("Rodzic")
    await page.getByLabel("Email").fill(email)
    await page.getByLabel("Hasło", { exact: true }).fill(PASSWORD)
    await page.getByLabel("Powtórz hasło").fill(PASSWORD)
    await page.getByRole("checkbox", { name: /Akceptuję Regulamin/ }).check()
    await page.getByRole("button", { name: "Załóż konto" }).click()

    // Po rejestracji wraca na /join?code=… i dołącza automatycznie.
    await expect(page.getByText(/Dołączyłeś jako\s*Kibic \/ rodzic/)).toBeVisible()
    await expect(
      page.getByText("Organizator przypisze Cię do dziecka", { exact: false }),
    ).toBeVisible()
    await expect(page.getByRole("link", { name: "Portal turnieju" })).toHaveAttribute(
      "href",
      `/t/${PUBLIC_SLUG}`,
    )

    const user = (await mock.state()).users.find((u) => u.email === email)
    expect(user?.roles).toEqual(["Viewer@Tournament"])

    // Strona startowa: czeka na przypisanie do dziecka, bez panelu organizatora.
    await page.goto("/start")
    await expect(page.getByText("Czekasz na organizatora")).toBeVisible()
    await expect(page.getByText("Puchar E2E")).toBeVisible()
  })

  test("uczestnik może później zostać organizatorem", async ({ page }) => {
    const email = `trener.${Date.now()}@e2e.pl`
    await page.goto("/register")
    await page.getByLabel("Imię").fill("Tomek")
    await page.getByLabel("Nazwisko").fill("Trener")
    await page.getByLabel("Email").fill(email)
    await page.getByLabel("Hasło", { exact: true }).fill(PASSWORD)
    await page.getByLabel("Powtórz hasło").fill(PASSWORD)
    await page.getByRole("checkbox", { name: /Akceptuję Regulamin/ }).check()
    await page.getByText("Dołączam do turnieju").click()
    await page.getByRole("button", { name: "Załóż konto" }).click()
    await expect(page).toHaveURL(/\/start$/)

    await page.getByRole("button", { name: "Zostań organizatorem" }).click()
    const dialog = page.getByRole("dialog")
    await expect(dialog.getByLabel("Nazwa organizacji")).toHaveValue("Tomek Trener")
    await dialog.getByLabel("Nazwa organizacji").fill("UKS Tomek")
    await dialog.getByRole("button", { name: "Utwórz organizację" }).click()

    await expect(page).toHaveURL(/\/tournaments$/)
    await expect(page.getByRole("link", { name: "Nowy turniej" }).first()).toBeVisible()
    expect((await mock.state()).organizations).toContain("UKS Tomek")
  })
})
