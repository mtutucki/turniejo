import { expect, loginAs, mock, ORGANIZER, test } from "./fixtures"

type Page = import("@playwright/test").Page

const TOURNAMENT = "0f000000-0000-4000-8000-000000000010"
const HOME_TEAM = "0f000000-0000-4000-8000-000000000050"
const CATEGORY = "0f000000-0000-4000-8000-000000000020"
const SQUAD_URL = `/tournaments/${TOURNAMENT}/teams/${HOME_TEAM}`

async function fillPlayer(page: Page, p: { first: string; last: string; born: string; shirt?: string }) {
  const dialog = page.getByRole("dialog", { name: "Nowy zawodnik" })
  await dialog.getByLabel("Imię").fill(p.first)
  await dialog.getByLabel("Nazwisko").fill(p.last)
  await dialog.getByLabel("Data urodzenia").fill(p.born)
  if (p.shirt) await dialog.getByLabel("Numer koszulki (opcjonalnie)").fill(p.shirt)
  await dialog.getByRole("button", { name: "Dodaj do składu" }).click()
  return dialog
}

test.describe("Skład drużyny w turnieju", () => {
  test.beforeEach(async ({ page }) => {
    await loginAs(page, ORGANIZER)
  })

  test("[PUC-006] zawodnicy z numerami, zajęty numer odrzucony", async ({ page }) => {
    await page.goto(SQUAD_URL)
    for (const [first, shirt] of [["Bolek", "7"], ["Lolek", "8"]] as const) {
      await page.getByRole("button", { name: "Dodaj zawodnika" }).click()
      await expect(await fillPlayer(page, { first, last: "Nowy", born: "2014-05-05", shirt })).toBeHidden()
    }
    await expect(page.getByText("3 zawodników")).toBeVisible()

    await page.getByRole("button", { name: "Dodaj zawodnika" }).click()
    const dialog = await fillPlayer(page, { first: "Tola", last: "Nowa", born: "2014-05-05", shirt: "7" })
    await expect(dialog.getByText("Ten numer jest już zajęty w drużynie")).toBeVisible()
  })

  test("[DRU-013] wiek spoza kategorii odrzucony, granica przechodzi", async ({ page }) => {
    await mock.override({
      method: "GET",
      path: "/api/tournaments/:id/categories",
      body: [
        { id: CATEGORY, tournamentId: TOURNAMENT, name: "U12", minBirthDate: "2014-01-01T00:00:00Z", maxBirthDate: "2014-12-31T00:00:00Z", order: 1 },
      ],
    })
    await page.goto(SQUAD_URL)
    await page.getByRole("button", { name: "Dodaj zawodnika" }).click()

    let dialog = await fillPlayer(page, { first: "Stary", last: "Gracz", born: "2013-12-31" })
    await expect(dialog.getByText("Za stary na kategorię U12")).toBeVisible()
    dialog = await fillPlayer(page, { first: "Młody", last: "Gracz", born: "2015-01-01" })
    await expect(dialog.getByText("Za młody na kategorię U12")).toBeVisible()
    dialog = await fillPlayer(page, { first: "Graniczny", last: "Gracz", born: "2014-01-01" })
    await expect(dialog).toBeHidden()
    await expect(page.getByText("Graniczny Gracz").first()).toBeVisible()
  })

  test("[DRU-015] te same dane co zawodnik w składzie: pytanie, bez potwierdzenia nic nie przybywa", async ({ page }) => {
    await page.goto(SQUAD_URL)
    await page.getByRole("button", { name: "Dodaj zawodnika" }).click()

    const dialog = await fillPlayer(page, { first: "Adam", last: "Bramka", born: "2014-03-01" })
    await expect(dialog.getByText(/W składzie jest już Adam Bramka/)).toBeVisible()
    expect(await mock.calls("POST", "/players/new")).toBe(0)

    await dialog.getByRole("button", { name: "Tak, dodaj" }).click()
    await expect(dialog).toBeHidden()
    await expect(page.getByText("2 zawodników")).toBeVisible()
  })

  test("[DRU-016] numer, pozycja i status zmienione, usunięcie ze składu", async ({ page }) => {
    await page.goto(SQUAD_URL)
    await page.getByRole("button", { name: "Edytuj Adam Bramka" }).click()
    const dialog = page.getByRole("dialog", { name: "Edycja zawodnika" })
    await dialog.getByLabel("Numer koszulki (opcjonalnie)").fill("12")
    await dialog.getByRole("combobox", { name: "Status" }).click()
    await page.getByRole("option", { name: "Kontuzjowany" }).click()
    await dialog.getByRole("button", { name: "Zapisz" }).click()
    await expect(dialog).toBeHidden()
    const row = page.getByRole("listitem").filter({ hasText: "Adam Bramka" })
    await expect(row).toContainText("12")
    await expect(row).toContainText("Kontuzjowany")

    await page.getByRole("button", { name: "Edytuj Adam Bramka" }).click()
    await dialog.getByRole("button", { name: "Usuń ze składu" }).click()
    const confirm = page.getByRole("alertdialog")
    if (await confirm.isVisible().catch(() => false)) await confirm.getByRole("button", { name: /Usuń/ }).click()
    await expect(page.getByText("Skład jest pusty")).toBeVisible()
  })

  test("[DRU-018] import z kadry: powtórzony i zajęty numer blokują dodanie", async ({ page }) => {
    await mock.override({
      method: "GET",
      path: "/api/tournaments/:tid/tournament-teams/:ttid/roster-sources",
      body: [
        {
          tournamentTeamId: null, tournamentId: null, tournamentName: "Kadra drużyny", tournamentStartAt: null, categoryName: null, kind: "Roster",
          players: [
            { playerId: "p-1", firstName: "Bartek", lastName: "Lewy", birthDate: "2014-07-12T00:00:00Z", shirtNumber: 4, position: "Defender", alreadyInSquad: false, ineligibility: null },
            { playerId: "p-2", firstName: "Daniel", lastName: "Prawy", birthDate: "2014-08-12T00:00:00Z", shirtNumber: 5, position: "Defender", alreadyInSquad: false, ineligibility: null },
          ],
        },
      ],
    })
    await page.goto(SQUAD_URL)
    await page.getByRole("button", { name: "Dodaj z kadry" }).click()
    const dialog = page.getByRole("dialog", { name: "Zawodnicy z kadry" })
    const add = dialog.getByRole("button", { name: /^Dodaj \d/ })

    await dialog.getByLabel("Numer koszulki: Daniel Prawy").fill("4")
    await expect(dialog.getByText("Numer się powtarza").first()).toBeVisible()
    await expect(add).toBeDisabled()

    await dialog.getByLabel("Numer koszulki: Daniel Prawy").fill("1")
    await expect(dialog.getByText("Numer zajęty w składzie")).toBeVisible()
    await expect(add).toBeDisabled()

    await dialog.getByLabel("Numer koszulki: Daniel Prawy").fill("5")
    await expect(add).toBeEnabled()
  })
})
