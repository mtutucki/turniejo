import { createTournament, expect, loginAs, mock, ORGANIZER, PASSWORD, PUBLIC_SLUG, REFEREE, test, TOURNAMENT_ID } from "./fixtures"

test.describe("Sesja i role", () => {
  test("[KON-005] wejście na panel bez logowania prowadzi do logowania i z powrotem", async ({ page }) => {
    const target = `/tournaments/${TOURNAMENT_ID}/teams`
    await page.goto(target)
    await expect(page).toHaveURL(/\/login\?callbackUrl=/)

    // Logowanie na tej samej stronie: adres powrotu jest w callbackUrl.
    await page.getByLabel("Email").fill(ORGANIZER)
    await page.getByLabel("Hasło").fill(PASSWORD)
    await page.getByRole("button", { name: "Zaloguj się" }).click()
    await expect(page).toHaveURL(new RegExp(`${target}$`))
  })

  test("[KON-044] po zmianie konta nie zostają dane poprzedniego", async ({ page }) => {
    await loginAs(page, ORGANIZER)
    const name = `Prywatny ${Date.now() % 100_000}`
    await createTournament(page, name)
    await page.goto("/tournaments")
    await expect(page.getByRole("link", { name })).toBeVisible()

    await page.getByRole("button", { name: "Menu użytkownika" }).click()
    await page.getByRole("menuitem", { name: /Wyloguj/ }).click()
    await expect(page).toHaveURL(/\/login/)
    await loginAs(page, REFEREE)
    await page.goto("/tournaments")
    await expect(page.getByText("Turniej: nie wybrano")).toBeVisible()
    await expect(page.getByText("Nie masz jeszcze organizacji")).toBeVisible()
    await expect(page.getByRole("link", { name })).toHaveCount(0)
  })

  test("[KON-045] kilka ról w jednym turnieju widać w nagłówku", async ({ page }) => {
    await mock.override({
      method: "GET",
      path: "/api/me/roles",
      body: [
        { role: "Referee", scopeType: "Tournament", scopeId: TOURNAMENT_ID },
        { role: "Coach", scopeType: "Tournament", scopeId: TOURNAMENT_ID },
      ],
    })
    await loginAs(page, REFEREE)
    await page.goto(`/tournaments/${TOURNAMENT_ID}/schedule`)

    await expect(page.getByText("Masz tu kilka ról:")).toBeVisible()
    await expect(page.getByRole("listitem").filter({ hasText: "Sędziujesz mecze w tym turnieju." })).toBeVisible()
    await expect(page.getByRole("listitem").filter({ hasText: "Prowadzisz drużynę w tym turnieju." })).toBeVisible()
  })
})

test.describe("Ustawienia turnieju", () => {
  test.beforeEach(async ({ page }) => {
    await loginAs(page, ORGANIZER)
  })

  test("[UST-003] edycja danych turnieju", async ({ page }) => {
    const id = await createTournament(page)
    await page.goto(`/tournaments/${id}/settings`)
    await page.getByRole("button", { name: "Dane turnieju" }).click()
    await page.getByLabel("Nazwa").fill("Nowa nazwa turnieju")
    await page.getByRole("button", { name: "Zapisz zmiany" }).click()

    await expect(page.getByRole("heading", { level: 1, name: "Nowa nazwa turnieju" })).toBeVisible()
    expect((await mock.state()).tournaments.map((t) => t.name)).toContain("Nowa nazwa turnieju")
  })

  test("[UST-007 UST-014] odrzucenie zmian i podwójny zapis zasad gry", async ({ page }) => {
    const id = await createTournament(page)
    await page.goto(`/tournaments/${id}/settings`)
    await page.getByRole("button", { name: "Zasady gry" }).click()

    await page.getByLabel("Wygrana").fill("5")
    await page.getByRole("button", { name: "Odrzuć zmiany" }).click()
    await expect(page.getByLabel("Wygrana")).toHaveValue("3")

    await page.getByLabel("Wygrana").fill("4")
    await page.getByRole("button", { name: "Zapisz zasady gry" }).dblclick()
    await expect(page.getByRole("button", { name: "Odrzuć zmiany" })).toBeHidden()
    expect(await mock.calls("PUT", "/settings")).toBe(1)
  })

  test("[UST-017] wyjście z niezapisanego kroku kreatora pyta o potwierdzenie", async ({ page }) => {
    const id = await createTournament(page)
    await page.goto(`/tournaments/${id}/setup?step=rules`)
    await page.getByLabel("Długość meczu").fill("45")

    await page.getByRole("list", { name: "Kroki konfiguracji" }).getByRole("button", { name: /Portal kibica/ }).click()
    const confirm = page.getByRole("alertdialog")
    await expect(confirm).toBeVisible()
    await expect(confirm.getByRole("heading", { name: "Masz niezapisane zmiany" })).toBeVisible()
    await confirm.getByRole("button", { name: "Zostaję" }).click()

    await expect(page.getByLabel("Długość meczu")).toHaveValue("45")
  })

  test("[UST-020] lista turniejów pogrupowana wg etapu", async ({ page }) => {
    await createTournament(page, "Szkic listy")
    await page.goto("/tournaments")

    await expect(page.getByRole("heading", { name: "Trwają" })).toBeVisible()
    await expect(page.getByRole("heading", { name: "Szkice" })).toBeVisible()
    const drafts = page.locator("section").filter({ has: page.getByRole("heading", { name: "Szkice" }) })
    await expect(drafts).toContainText("Szkic listy")
  })
})

test.describe("Portal kibica", () => {
  test("[PUB-009] wybrana drużyna zostaje po odświeżeniu", async ({ page }) => {
    await page.goto(`/t/${PUBLIC_SLUG}`)
    await page.getByRole("combobox", { name: "Drużyna" }).click()
    await page.getByRole("option", { name: "Lwy E2E" }).click()

    await page.reload()
    await expect(page.getByRole("combobox", { name: "Drużyna" })).toHaveText(/Lwy E2E/)
  })
})
