import type { Page } from "@playwright/test"

import { expect, login, mock, PASSWORD, test } from "./fixtures"

async function fillRegistration(page: Page, email: string, firstName = "Kamil") {
  await page.getByLabel("Imię").fill(firstName)
  await page.getByLabel("Nazwisko").fill("Testowy")
  await page.getByLabel("Email").fill(email)
  await page.getByLabel("Hasło", { exact: true }).fill(PASSWORD)
  await page.getByLabel("Powtórz hasło").fill(PASSWORD)
  await page.getByRole("checkbox", { name: /Akceptuję Regulamin/ }).check()
}

test.describe("Rejestracja → Logowanie", () => {
  test("organizator: konto z organizacją, wylogowanie i powrót hasłem", async ({ page }) => {
    const email = `organizator.${Date.now()}@e2e.pl`

    await page.goto("/register")
    await fillRegistration(page, email)
    await page.getByText("Organizuję turnieje").click()
    await page.getByRole("button", { name: "Załóż konto" }).click()

    // Po rejestracji automatyczne logowanie i od razu lista turniejów własnej organizacji.
    await expect(page).toHaveURL(/\/tournaments$/)
    await expect(page.getByRole("link", { name: "Nowy turniej" }).first()).toBeVisible()
    const user = (await mock.state()).users.find((u) => u.email === email)
    expect(user?.roles).toContain("Organizer@Organization")

    await page.getByRole("button", { name: "Menu użytkownika" }).first().click()
    await expect(page.getByRole("menu")).toContainText(email)
    await page.getByRole("menuitem", { name: "Wyloguj się" }).click()
    await expect(page).toHaveURL(/\/login/)

    // Strona prywatna bez sesji → logowanie z powrotem na tę stronę.
    await page.goto("/tournaments")
    await expect(page).toHaveURL(/\/login\?callbackUrl=.*%2Ftournaments$/)

    await login(page, email)
    await expect(page).toHaveURL(/\/tournaments$/)
  })

  test("uczestnik: bez organizacji, ekran powitalny zamiast panelu organizatora", async ({
    page,
  }) => {
    const email = `rodzic.${Date.now()}@e2e.pl`

    await page.goto("/register")
    await fillRegistration(page, email, "Anna")
    await page.getByText("Dołączam do turnieju").click()
    await page.getByRole("button", { name: "Załóż konto" }).click()

    await expect(page).toHaveURL(/\/start$/)
    await expect(page.getByRole("heading", { name: "Witaj w Turniejo, Anna!" })).toBeVisible()
    await expect(page.getByRole("link", { name: "Dołącz kodem" }).first()).toBeVisible()

    const state = await mock.state()
    expect(state.users.find((u) => u.email === email)?.roles).toEqual([])
    expect(state.organizations).not.toContain("Anna Testowy")
    // Menu bez sekcji organizatora.
    await expect(page.getByRole("link", { name: "Moje turnieje" })).toHaveCount(0)
  })

  test("walidacja formularza i błędne hasło", async ({ page }) => {
    await page.goto("/register")
    await page.getByLabel("Email").fill("zly-email")
    await page.getByLabel("Hasło", { exact: true }).fill("krotkie")
    await page.getByLabel("Powtórz hasło").fill("inne")
    await page.getByRole("button", { name: "Załóż konto" }).click()
    await expect(page.getByText("Nieprawidłowy adres email")).toBeVisible()
    await expect(page.getByText("Hasło musi mieć co najmniej 8 znaków")).toBeVisible()
    await expect(page.getByText("Hasła nie są takie same")).toBeVisible()
    // Typ konta trzeba wybrać świadomie — nie ma domyślnego "organizatora".
    await expect(page.getByText("Wybierz, jak chcesz korzystać z Turniejo")).toBeVisible()
    await expect(page.getByText("Zaakceptuj Regulamin, aby założyć konto")).toBeVisible()

    await login(page, "organizator@e2e.pl", "zle-haslo")
    await expect(page.getByText(/Nieprawidłowy email lub hasło/)).toBeVisible()
    await expect(page).toHaveURL(/\/login/)
  })

  test("zajęty email pokazuje błąd przy polu", async ({ page }) => {
    await page.goto("/register")
    await fillRegistration(page, "organizator@e2e.pl", "Olga")
    await page.getByText("Organizuję turnieje").click()
    await page.getByRole("button", { name: "Załóż konto" }).click()
    await expect(
      page.getByText(/email/i).and(page.locator("[data-slot=field-error]")),
    ).toBeVisible()
    await expect(page).toHaveURL(/\/register/)
  })
})

test.describe("RODO: konto", () => {
  test("uczestnik pobiera dane i usuwa konto — potem nie może się zalogować", async ({ page }) => {
    const email = `usun.${Date.now()}@e2e.pl`

    await page.goto("/register")
    await fillRegistration(page, email, "Ewa")
    await page.getByText("Dołączam do turnieju").click()
    await page.getByRole("button", { name: "Załóż konto" }).click()
    await expect(page).toHaveURL(/\/start$/)

    await page.goto("/account")
    const download = page.waitForEvent("download")
    await page.getByRole("button", { name: "Pobierz moje dane (JSON)" }).click()
    expect((await download).suggestedFilename()).toMatch(/^turniejo-moje-dane-.*\.json$/)

    await page.getByRole("button", { name: "Usuń konto" }).click()
    await page.getByRole("button", { name: "Usuń konto na zawsze" }).click()
    await expect(page).toHaveURL(/\/login/)
    expect((await mock.state()).users.some((u) => u.email === email)).toBe(false)

    await login(page, email)
    await expect(page.getByText(/Nieprawidłowy email lub hasło/)).toBeVisible()
  })

  test("jedyny organizator nie usunie konta samodzielnie", async ({ page }) => {
    await login(page, "organizator@e2e.pl")
    await expect(page).toHaveURL(/\/tournaments$/)

    await page.goto("/account")
    await page.getByRole("button", { name: "Usuń konto" }).click()
    await page.getByRole("button", { name: "Usuń konto na zawsze" }).click()
    await expect(page.getByRole("alert").filter({ hasText: "jedynym organizatorem" })).toBeVisible()
    await expect(page).toHaveURL(/\/account$/)
  })
})
