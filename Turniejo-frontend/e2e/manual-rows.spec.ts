// Wiersze arkusza, które w ręcznym QA były „ręczne” (wygląd, telefon, sieć) - to, co da się sprawdzić automatem.
import { expect, login, loginAs, MATCH_ID, mock, ORGANIZER, PUBLIC_SLUG, REFEREE, test, TOURNAMENT_ID } from "./fixtures"

type Page = import("@playwright/test").Page

const noHorizontalScroll = async (page: Page) =>
  expect(await page.evaluate(() => document.documentElement.scrollWidth)).toBeLessThanOrEqual(page.viewportSize()!.width)

test("[KON-003] złe hasło i nieistniejące konto dają ten sam komunikat", async ({ page }) => {
  for (const email of [ORGANIZER, "nie-ma-takiego@e2e.pl"]) {
    await login(page, email, "ZleHaslo123!")
    await expect(page.getByText(/Nieprawidłowy email lub hasło/)).toBeVisible()
    await expect(page).toHaveURL(/\/login/)
  }
})

test("[KON-040] telefon dłuższy niż 20 znaków i e-mail bez edycji", async ({ page }) => {
  await loginAs(page, REFEREE)
  await page.goto("/account")
  await page.getByRole("button", { name: "Edytuj" }).click()

  await expect(page.getByLabel("E-mail")).toBeDisabled()
  await page.getByLabel("Telefon (opcjonalnie)").fill("1".repeat(21))
  await page.getByRole("button", { name: "Zapisz" }).click()

  await expect(page.getByText("Maksymalnie 20 znaków")).toBeVisible()
  expect(await mock.calls("PUT", "/api/me/profile")).toBe(0)
})

test("[DRU-023] polskie znaki i bardzo długa nazwa drużyny nie psują układu", async ({ page }) => {
  await loginAs(page, ORGANIZER)
  await page.goto("/teams")
  const longName = "Klub Sportowy Bardzo Długiej Nazwy Która Nie Powinna Rozjechać Układu Strony Na Telefonie Ani Na Komputerze 2026"
  for (const name of ["Żółć Gęślą Jaźń Ęą", longName]) {
    await page.getByRole("button", { name: "Nowa drużyna" }).click()
    const dialog = page.getByRole("dialog", { name: "Nowa drużyna" })
    await dialog.getByLabel("Nazwa").fill(name)
    await dialog.getByRole("button", { name: "Zapisz" }).click()
    await expect(dialog).toBeHidden()
  }

  await expect(page.getByRole("link", { name: /Żółć Gęślą Jaźń Ęą/ })).toBeVisible()
  await expect(page.getByRole("link", { name: /Klub Sportowy Bardzo Długiej/ })).toBeVisible()
  await noHorizontalScroll(page)
})

test("[GRP-008] przy otwartych zapisach terminarza nie da się wygenerować", async ({ page }) => {
  await mock.tournament(TOURNAMENT_ID, { status: "RegistrationOpen" })
  await mock.override({ method: "GET", path: "/api/tournaments/:id/schedule", body: [] })
  await loginAs(page, ORGANIZER)
  await page.goto(`/tournaments/${TOURNAMENT_ID}/schedule/generate`)

  const condition = page.getByRole("listitem").filter({ hasText: "Zapisy są wciąż otwarte" })
  await expect(condition.getByLabel("Niespełnione")).toBeVisible()
  await expect(condition).toContainText("ostatecznej listy drużyn")
  await expect(page.getByRole("button", { name: "Generuj terminarz" })).toBeDisabled()
})

test("[MNZ-024] gol bez sieci zapisuje się raz po powrocie połączenia", async ({ page }) => {
  await loginAs(page, REFEREE)
  await page.goto(`/matches/${MATCH_ID}/live`)
  await expect(page.getByRole("status").filter({ hasText: "Na żywo" })).toBeVisible()

  await page.context().setOffline(true)
  await page.getByRole("button", { name: "Szybki gol: ORŁ" }).click()
  // Gol czeka na sieć z widocznym znacznikiem, nic nie poszło do serwera.
  await expect(page.getByRole("list", { name: "Zdarzenia meczu" }).getByRole("img", { name: "Zapisywanie" })).toBeVisible()
  expect(await mock.calls("POST", "/goals")).toBe(0)

  await page.context().setOffline(false)
  await expect.poll(() => mock.calls("POST", "/goals")).toBe(1)
  await expect.poll(async () => (await mock.state()).matches[0]!.homeScore).toBe(1)
  await page.reload()
  await expect(page.getByLabel("Orły E2E: 1", { exact: true })).toBeVisible()
})

test("[PAN-015] terminarz organizatora pokazuje mecze bez sędziego", async ({ page }) => {
  await loginAs(page, ORGANIZER)
  await page.goto(`/tournaments/${TOURNAMENT_ID}/schedule`)
  const match = page.getByRole("listitem").filter({ hasText: "Orły E2E" })
  await expect(match).toContainText("Sędzia przypisany")
  await expect(match).toContainText("Na żywo")

  await mock.release(MATCH_ID)
  await page.reload()
  await expect(page.getByRole("listitem").filter({ hasText: "Orły E2E" })).toContainText("Bez sędziego")
})

test("[PUB-012] odwołany turniej: czytelna informacja, bez wskaźnika na żywo", async ({ page }) => {
  await mock.tournament(TOURNAMENT_ID, { status: "Cancelled" })
  await mock.matchStatus(MATCH_ID, "Cancelled")
  await page.goto(`/t/${PUBLIC_SLUG}`)

  await expect(page.getByText("Odwołany").first()).toBeVisible()
  await expect(page.getByText("1 mecz został odwołany.")).toBeVisible()
  await expect(page.getByRole("banner").getByRole("status")).toHaveCount(0)

  // Odwołany przed ułożeniem terminarza: bez obietnicy, że terminarz „pojawi się wkrótce”.
  await mock.override({ method: "GET", path: `/public/tournaments/${PUBLIC_SLUG}/schedule`, body: [] })
  await page.reload()
  await expect(page.getByText("Turniej został odwołany")).toBeVisible()
  await expect(page.getByText("Organizator odwołał ten turniej. Mecze się nie odbędą.")).toBeVisible()
})

test("[PUB-013] portal na telefonie bez przewijania w bok", async ({ page }, testInfo) => {
  test.skip(testInfo.project.name !== "mobile", "układ na telefonie")
  for (const path of ["", "/schedule", "/standings", "/teams", "/stats", `/matches/${MATCH_ID}`]) {
    await page.goto(`/t/${PUBLIC_SLUG}${path}`)
    await expect(page.getByRole("heading", { level: 1 }).first()).toBeVisible()
    await noHorizontalScroll(page)
  }
})
