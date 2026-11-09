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
  test("[KON-001 KON-038b] organizator: konto z organizacją, wylogowanie i powrót hasłem", async ({ page }) => {
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

  test("[KON-038a KON-038c] walidacja formularza i błędne hasło", async ({ page }) => {
    await page.goto("/register")
    await page.getByLabel("Email").fill("zly-email")
    await page.getByLabel("Hasło", { exact: true }).fill("krotkie")
    await page.getByLabel("Powtórz hasło").fill("inne")
    await page.getByRole("button", { name: "Załóż konto" }).click()
    await expect(page.getByText("Nieprawidłowy adres email")).toBeVisible()
    await expect(page.getByText("Hasło musi mieć co najmniej 10 znaków")).toBeVisible()
    await expect(page.getByText("Hasła nie są takie same")).toBeVisible()
    // Typ konta trzeba wybrać świadomie — nie ma domyślnego "organizatora".
    await expect(page.getByText("Wybierz, jak chcesz korzystać z Turniejo")).toBeVisible()
    await expect(page.getByText("Zaakceptuj Regulamin, aby założyć konto")).toBeVisible()

    // Długie hasło bez cyfry też nie przechodzi.
    await page.getByLabel("Hasło", { exact: true }).fill("Abcdefghij")
    await page.getByLabel("Powtórz hasło").fill("Abcdefghij")
    await page.getByRole("button", { name: "Załóż konto" }).click()
    await expect(page.getByText("Hasło musi zawierać małą i wielką literę oraz cyfrę")).toBeVisible()

    await login(page, "organizator@e2e.pl", "zle-haslo")
    await expect(page.getByText(/Nieprawidłowy email lub hasło/)).toBeVisible()
    await expect(page).toHaveURL(/\/login/)
  })

  test("[KON-002] zajęty email pokazuje błąd przy polu", async ({ page }) => {
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

test.describe("Reset hasła", () => {
  test("[KON-004] link z maila: nowe hasło w aplikacji, stare przestaje działać, link jest jednorazowy", async ({
    page,
  }) => {
    const link = "/password-reset/confirm?token=token-organizator@e2e.pl"
    const newPassword = "NoweHaslo456!"

    await page.goto(link)
    await page.getByLabel("Nowe hasło").fill(newPassword)
    await page.getByLabel("Powtórz hasło").fill("inne-haslo")
    await page.getByRole("button", { name: "Ustaw hasło" }).click()
    await expect(page.getByText("Hasła nie są takie same")).toBeVisible()

    await page.getByLabel("Powtórz hasło").fill(newPassword)
    await page.getByRole("button", { name: "Ustaw hasło" }).click()
    await expect(page.getByText("Hasło zostało zmienione")).toBeVisible()

    await page.getByRole("link", { name: "Zaloguj się" }).click()
    await expect(page).toHaveURL(/\/login$/)
    await login(page, "organizator@e2e.pl")
    await expect(page.getByText(/Nieprawidłowy email lub hasło/)).toBeVisible()
    await login(page, "organizator@e2e.pl", newPassword)
    await expect(page).toHaveURL(/\/tournaments$/)

    // Wylogowany user z tym samym linkiem — token jest już zużyty.
    await page.context().clearCookies()
    await page.goto(link)
    await page.getByLabel("Nowe hasło").fill("JeszczeInne789!")
    await page.getByLabel("Powtórz hasło").fill("JeszczeInne789!")
    await page.getByRole("button", { name: "Ustaw hasło" }).click()
    await expect(
      page.getByRole("alert").filter({ hasText: "wygasł albo został już użyty" }),
    ).toBeVisible()
    await expect(page.getByRole("link", { name: "Wyślij nowy link" })).toBeVisible()
  })

  test("[KON-004] prośba o reset: ten sam komunikat dla istniejącego i nieznanego e-maila", async ({ page }) => {
    for (const email of ["organizator@e2e.pl", "nie-ma-takiego@e2e.pl"]) {
      await page.goto("/password-reset")
      await page.getByLabel("Email").fill(email)
      await page.getByRole("button", { name: "Wyślij link" }).click()
      await expect(page.getByText(`Jeśli konto ${email} istnieje`, { exact: false })).toBeVisible()
    }
  })

  test("[KON-039] link bez tokenu nie pokazuje formularza", async ({ page }) => {
    await page.goto("/password-reset/confirm")
    await expect(page.getByText("Ten link jest niepełny")).toBeVisible()
    await expect(page.getByRole("button", { name: "Ustaw hasło" })).toHaveCount(0)
  })
})

test.describe("Organizacja", () => {
  test("[KON-042] organizator nazywa organizację przy rejestracji i zmienia nazwę w koncie", async ({
    page,
  }) => {
    const email = `klub.${Date.now()}@e2e.pl`
    await page.goto("/register")
    await fillRegistration(page, email, "Olga")
    await page.getByText("Organizuję turnieje").click()
    await page.getByLabel("Nazwa organizacji (opcjonalnie)").fill("UKS Orły")
    await page.getByRole("button", { name: "Załóż konto" }).click()
    await expect(page.getByText("Organizacja: UKS Orły")).toBeVisible()

    await page.goto("/account")
    await page.getByRole("button", { name: "Zmień nazwę" }).click()
    const dialog = page.getByRole("dialog", { name: "Nazwa organizacji" })
    await dialog.getByLabel("Nazwa organizacji").fill("Akademia Orły")
    await dialog.getByRole("button", { name: "Zapisz" }).click()
    await expect(page.getByText("Akademia Orły")).toBeVisible()
    expect((await mock.state()).organizations).toContain("Akademia Orły")
  })
})

test.describe("RODO: konto", () => {
  test("[KON-008] uczestnik pobiera dane i usuwa konto — potem nie może się zalogować", async ({ page }) => {
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

  test("[KON-009] jedyny organizator usuwa konto z organizacją dopiero po drugim potwierdzeniu", async ({
    page,
  }) => {
    await login(page, "organizator@e2e.pl")
    await expect(page).toHaveURL(/\/tournaments$/)

    await page.goto("/account")
    await page.getByRole("button", { name: "Usuń konto" }).click()
    await page.getByRole("button", { name: "Usuń konto na zawsze" }).click()

    // Pierwsze potwierdzenie nie wystarcza — konto i organizacja wciąż istnieją.
    const confirm = page.getByRole("alertdialog", { name: "Usunąć też całą organizację?" })
    await expect(confirm).toContainText("jedynym organizatorem")
    const submit = confirm.getByRole("button", { name: "Usuń konto i organizację" })
    await expect(submit).toBeDisabled()
    expect((await mock.state()).users.some((u) => u.email === "organizator@e2e.pl")).toBe(true)

    await confirm.getByLabel("Aby potwierdzić, wpisz USUŃ").fill("USUŃ")
    await submit.click()
    await expect(page).toHaveURL(/\/login/)
    expect((await mock.state()).users.some((u) => u.email === "organizator@e2e.pl")).toBe(false)
  })
})
