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

test("[PUC-033a] przeniesienie meczu na zajęte boisko", async ({ page }) => {
  await mock.matchStatus("0f000000-0000-4000-8000-000000000060", "Scheduled")
  await mock.override({
    method: "PUT",
    path: "/api/tournaments/:tid/matches/:mid/schedule",
    status: 400,
    body: { code: "Validation.ScheduledAt", message: "Another match is already scheduled on this pitch at this time", type: 2 },
  })
  await loginAs(page, ORGANIZER)
  await page.goto(`/tournaments/${TOURNAMENT_ID}/schedule`)
  await page.getByRole("button", { name: "Zmień termin: Orły E2E – Lwy E2E" }).click()
  const dialog = page.getByRole("dialog", { name: "Zmień termin" })
  await dialog.getByRole("button", { name: "Zapisz" }).click()

  await expect(page.getByText("Na tym boisku trwa wtedy inny mecz")).toBeVisible()
})

test("zmiana terminu domyślnie przesuwa kolejne mecze na tym boisku", async ({ page }) => {
  const matchId = "0f000000-0000-4000-8000-000000000060"
  await mock.matchStatus(matchId, "Scheduled")
  await mock.override({
    method: "PUT",
    path: "/api/tournaments/:tid/matches/:mid/schedule",
    body: { matchId },
  })
  await loginAs(page, ORGANIZER)
  await page.goto(`/tournaments/${TOURNAMENT_ID}/schedule`)
  const open = page.getByRole("button", { name: "Zmień termin: Orły E2E – Lwy E2E" })
  const dialog = page.getByRole("dialog", { name: "Zmień termin" })
  const shift = dialog.getByRole("checkbox", { name: "Przesuń też kolejne mecze na tym boisku" })
  const saved = () =>
    page.waitForRequest((r) => r.method() === "PUT" && r.url().endsWith(`/matches/${matchId}/schedule`))

  await open.click()
  await expect(shift).toBeChecked()
  let request = saved()
  await dialog.getByRole("button", { name: "Zapisz" }).click()
  expect((await request).postDataJSON()).toMatchObject({ shiftFollowing: true })
  await expect(dialog).toBeHidden()

  await open.click()
  await shift.click()
  request = saved()
  await dialog.getByRole("button", { name: "Zapisz" }).click()
  expect((await request).postDataJSON()).toMatchObject({ shiftFollowing: false })
  await expect(dialog).toBeHidden()

  // Inne boisko: kolejne mecze zostają, opcji nie ma.
  await open.click()
  await dialog.getByRole("combobox", { name: "Boisko" }).click()
  await page.getByRole("option", { name: "Bez boiska" }).click()
  await expect(shift).toBeHidden()
})
