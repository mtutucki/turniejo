import { expect, loginAs, mock, test } from "./fixtures"

const COACH = "trener@e2e.pl"
const PARENT = "rodzic@e2e.pl"
const HOME_TEAM = "0f000000-0000-4000-8000-000000000050"
const AWAY_TEAM = "0f000000-0000-4000-8000-000000000051"
const CHILD = "0f000000-0000-4000-8000-0000000000b1"
const OTHER_PLAYER = "0f000000-0000-4000-8000-0000000000b2"

test.describe("Panel trenera", () => {
  test("[PAN-001] trener widzi swoją drużynę, a cudzej nie", async ({ page }) => {
    await loginAs(page, COACH)
    await page.goto("/coach/teams")
    await expect(page.getByRole("heading", { name: "Moje drużyny" })).toBeVisible()

    await page.getByRole("link", { name: /Orły E2E/ }).click()
    await expect(page).toHaveURL(new RegExp(`/coach/teams/${HOME_TEAM}/players$`))
    await expect(page.getByRole("heading", { level: 1, name: "Orły E2E" })).toBeVisible()
    await expect(page.getByText("Adam")).toBeVisible()

    await page.goto(`/coach/teams/${AWAY_TEAM}/players`)
    await expect(page.getByText("Brak dostępu do tej drużyny")).toBeVisible()
  })
})

test.describe("Panel rodzica", () => {
  test("[PAN-004 PAN-005] rodzic widzi dziecko, jego terminarz i tabelę", async ({ page }) => {
    await loginAs(page, PARENT)
    await page.goto("/parent/players")
    await expect(page.getByRole("heading", { name: "Moje dzieci" })).toBeVisible()

    await page.getByRole("link", { name: /Adam Bramka/ }).click()
    await expect(page).toHaveURL(new RegExp(`/parent/players/${CHILD}/schedule$`))
    await expect(page.getByText("Lwy E2E").first()).toBeVisible()

    await page.getByRole("link", { name: "Tabela", exact: true }).click()
    await expect(page.getByText("Grupa A").first()).toBeVisible()
    await expect(page.getByText("Orły E2E").first()).toBeVisible()

    await page.goto(`/parent/players/${OTHER_PLAYER}/schedule`)
    await expect(page.getByText("Brak dostępu do tego zawodnika")).toBeVisible()
  })

  test("[PAN-006] rodzic poprawia dane dziecka", async ({ page }) => {
    await loginAs(page, PARENT)
    await page.goto(`/parent/players/${CHILD}/schedule`)

    await page.getByRole("button", { name: "Edytuj profil" }).click()
    const dialog = page.getByRole("dialog", { name: "Profil zawodnika" })
    await dialog.getByLabel("Imię").fill("Adaś")
    await dialog.getByRole("button", { name: "Zapisz" }).click()

    await expect(page.getByText("Profil zapisany")).toBeVisible()
    await expect(page.getByRole("heading", { level: 1 })).toContainText("Adaś")
  })
})

test("[PAN-003] trener dodaje zawodnika przed startem turnieju", async ({ page }) => {
  await mock.tournament("0f000000-0000-4000-8000-000000000010", { status: "RegistrationClosed" })
  await loginAs(page, COACH)
  await page.goto(`/coach/teams/${HOME_TEAM}/players`)

  await page.getByRole("button", { name: "Dodaj zawodnika" }).click()
  const dialog = page.getByRole("dialog", { name: "Nowy zawodnik" })
  await dialog.getByLabel("Imię").fill("Kamil")
  await dialog.getByLabel("Nazwisko").fill("Trenerski")
  await dialog.getByLabel("Data urodzenia").fill("2014-04-04")
  await dialog.getByRole("button", { name: "Dodaj do składu" }).click()

  await expect(dialog).toBeHidden()
  await expect(page.getByText("Kamil Trenerski", { exact: true })).toBeVisible()
})

test("[PAN-014] rodzic w pucharze: bez zakładki tabeli", async ({ page }) => {
  await mock.override({ method: "GET", path: "/api/parent/players/:id/standing", body: [] })
  await loginAs(page, PARENT)
  await page.goto(`/parent/players/${CHILD}/schedule`)

  await expect(page.getByRole("link", { name: "Wyniki", exact: true })).toBeVisible()
  await expect(page.getByRole("link", { name: "Tabela", exact: true })).toHaveCount(0)
})
