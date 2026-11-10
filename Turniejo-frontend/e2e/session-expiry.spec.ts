import { expect, login, loginAs, PUBLIC_SLUG, test } from "./fixtures"

test.describe("Wygaśnięcie sesji", () => {
  test("[KON-006 MNZ-025] po wygaśnięciu sesji wraca do logowania raz, bez pętli przeładowań", async ({ page }) => {
    test.setTimeout(90_000)
    // Token żyje 35 s, a odświeżenie się nie udaje (jak po restarcie Keycloaka).
    await loginAs(page, "wygasa@e2e.pl")
    await expect(page).not.toHaveURL(/\/login/)

    let navigations = 0
    page.on("framenavigated", (frame) => {
      if (frame === page.mainFrame()) navigations++
    })

    // Sesja wygasa → jedno przejście na logowanie.
    await expect(page).toHaveURL(/\/login/, { timeout: 45_000 })
    await page.waitForTimeout(1_000)
    const settled = navigations
    await page.waitForTimeout(5_000)

    // Strona logowania stoi: brak kolejnych przeładowań i zagnieżdżonych callbackUrl.
    expect(navigations - settled).toBe(0)
    const url = decodeURIComponent(decodeURIComponent(page.url()))
    expect(url.match(/callbackUrl/g)?.length ?? 0).toBeLessThanOrEqual(1)
    expect(url).not.toMatch(/callbackUrl=[^&]*\/login/)
    await expect(page.getByRole("button", { name: "Zaloguj się" })).toBeVisible()
  })

  test("wygaśnięcie sesji w portalu kibica zostawia kibica w portalu, już wylogowanego", async ({ page }) => {
    test.setTimeout(90_000)
    await loginAs(page, "wygasa@e2e.pl")
    await page.goto(`/t/${PUBLIC_SLUG}`)
    await expect(page.getByRole("link", { name: "Mój panel" })).toBeVisible()

    // Sesja wygasa (token żyje 35 s): portal jest publiczny, więc zostajemy na nim.
    await expect(page.getByRole("link", { name: "Zaloguj się" })).toBeVisible({ timeout: 45_000 })
    await page.waitForTimeout(3_000)
    await expect(page).toHaveURL(new RegExp(`/t/${PUBLIC_SLUG}$`))
  })
})

test.describe("Nieważna sesja w ciasteczku", () => {
  test("strona logowania nie zapętla się, gdy sesja jest od razu nieważna", async ({ page }) => {
    let navigations = 0
    page.on("framenavigated", (frame) => {
      if (frame === page.mainFrame()) navigations++
    })

    // Token żyje 1 s: proxy odsyła na /login z uszkodzoną sesją — wcześniej 80+ przeładowań w 4 s.
    await login(page, "wygasa-od-razu@e2e.pl")
    await expect(page).toHaveURL(/\/login/)
    await page.waitForTimeout(2_000)
    const settled = navigations
    await page.waitForTimeout(4_000)

    expect(navigations - settled).toBe(0)
    const url = decodeURIComponent(decodeURIComponent(page.url()))
    expect(url).not.toMatch(/callbackUrl=[^&]*\/login/)
    // Formularz gotowy do ponownego logowania (nie wisi na "Logowanie…").
    await expect(page.getByRole("button", { name: "Zaloguj się" })).toBeEnabled()
  })
})
