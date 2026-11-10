import { createTournament, expect, loginAs, ORGANIZER, test } from "./fixtures"

type Page = import("@playwright/test").Page

const steps = (page: Page) => page.getByRole("list", { name: "Kroki konfiguracji" })

async function addInDialog(page: Page, opener: string, name: string) {
  await page.getByRole("button", { name: opener }).first().click()
  const dialog = page.getByRole("dialog")
  await dialog.getByLabel("Nazwa").fill(name)
  await dialog.getByRole("button", { name: /Dodaj|Zapisz/ }).last().click()
  return dialog
}

test.describe("Kreator konfiguracji", () => {
  test.beforeEach(async ({ page }) => {
    await loginAs(page, ORGANIZER)
  })

  test("[UST-016 PUC-002 PUC-003 PUC-004] puchar z meczem o 3. miejsce, kategorią i boiskiem", async ({ page }) => {
    await createTournament(page)
    // Kreator startuje sam po utworzeniu turnieju, od formatu.
    await expect(page.getByRole("group", { name: "Format turnieju" })).toBeVisible()

    await page.getByText("Pucharowy", { exact: true }).click()
    await expect(page.getByRole("radio", { name: /Pucharowy/ })).toBeChecked()
    await page.getByRole("button", { name: "Zapisz i dalej" }).click()

    await page.getByRole("switch", { name: "Mecz o 3. miejsce" }).click()
    await page.getByRole("button", { name: "Zapisz i dalej" }).click()

    await expect(page.getByRole("switch", { name: "Turniej publiczny" })).toBeVisible()
    await page.getByRole("button", { name: "Pomiń ten krok" }).click()

    const category = await addInDialog(page, "Dodaj kategorię", "U12")
    await expect(category).toBeHidden()
    await expect(page.getByText("U12").first()).toBeVisible()
    await page.getByRole("button", { name: /^Dalej/ }).first().click()

    const pitch = await addInDialog(page, "Dodaj boisko", "Boisko 1")
    await expect(pitch).toBeHidden()
    await expect(page.getByText("Boisko 1").first()).toBeVisible()

    await steps(page).getByRole("button", { name: /Terminarz/ }).click()
    const checklist = page.getByRole("list").filter({ hasText: "Format rozgrywek" })
    await expect(checklist.getByRole("listitem").filter({ hasText: "Format rozgrywek" })).not.toContainText("Nie wybrano")
    await expect(checklist.getByRole("listitem").filter({ hasText: "Kategorie" })).not.toContainText("Brak kategorii")
    await expect(checklist.getByRole("listitem").filter({ hasText: "Boiska" })).not.toContainText("Brak boisk")

    // Ustawienie meczu o 3. miejsce zostało zapisane.
    await steps(page).getByRole("button", { name: /Zasady gry/ }).click()
    await expect(page.getByRole("switch", { name: "Mecz o 3. miejsce" })).toBeChecked()
  })
})

test.describe("Unikalne nazwy", () => {
  test.beforeEach(async ({ page }) => {
    await loginAs(page, ORGANIZER)
  })

  test("[GRP-002b] druga kategoria o tej samej nazwie", async ({ page }) => {
    const id = await createTournament(page)
    await page.goto(`/tournaments/${id}/setup?step=categories`)
    await expect(await addInDialog(page, "Dodaj kategorię", "U12")).toBeHidden()

    const dialog = await addInDialog(page, "Dodaj kategorię", "U12")
    await expect(dialog.getByText("Kategoria o tej nazwie już istnieje")).toBeVisible()
  })

  test("[GRP-002c] drugie boisko o tej samej nazwie", async ({ page }) => {
    const id = await createTournament(page)
    await page.goto(`/tournaments/${id}/setup?step=pitches`)
    await expect(await addInDialog(page, "Dodaj boisko", "Boisko 1")).toBeHidden()

    const dialog = await addInDialog(page, "Dodaj boisko", "Boisko 1")
    await expect(dialog.getByText("Boisko o tej nazwie już istnieje")).toBeVisible()
  })

  test("[GRP-001 GRP-002a] format grupowy: Grupa A sama, druga grupa dodana, duplikat odrzucony", async ({ page }) => {
    const id = await createTournament(page)
    await page.getByText("Grupowy", { exact: true }).click()
    await page.getByRole("button", { name: "Zapisz i dalej" }).click()
    await expect(page.getByRole("spinbutton", { name: "Wygrana" })).toBeVisible()
    await page.goto(`/tournaments/${id}/categories`)
    await expect(await addInDialog(page, "Dodaj kategorię", "U12")).toBeHidden()
    await expect(page.getByRole("button", { name: "Edytuj Grupa A" })).toBeVisible()

    // Proponowana nazwa to kolejna litera.
    await page.getByRole("button", { name: "Dodaj grupę" }).click()
    let dialog = page.getByRole("dialog", { name: "Nowa grupa · U12" })
    await expect(dialog.getByLabel("Nazwa")).toHaveValue("Grupa B")
    await dialog.getByRole("button", { name: "Zapisz" }).click()
    await expect(dialog).toBeHidden()
    await expect(page.getByRole("button", { name: "Edytuj Grupa B" })).toBeVisible()

    await page.getByRole("button", { name: "Dodaj grupę" }).click()
    dialog = page.getByRole("dialog", { name: "Nowa grupa · U12" })
    await dialog.getByLabel("Nazwa").fill("Grupa A")
    await dialog.getByRole("button", { name: "Zapisz" }).click()
    await expect(dialog.getByText("Ta nazwa jest już zajęta")).toBeVisible()
  })
})

test.describe("Grupy w kategorii", () => {
  test.beforeEach(async ({ page }) => {
    await loginAs(page, ORGANIZER)
  })

  async function groupTournament(page: Page) {
    const id = await createTournament(page)
    await page.getByText("Grupowy", { exact: true }).click()
    await page.getByRole("button", { name: "Zapisz i dalej" }).click()
    await expect(page.getByRole("spinbutton", { name: "Wygrana" })).toBeVisible()
    await page.goto(`/tournaments/${id}/categories`)
    return id
  }

  test("[GRP-002] nowa kategoria od razu z wybraną liczbą grup", async ({ page }) => {
    await groupTournament(page)
    await page.getByRole("button", { name: "Dodaj kategorię" }).first().click()
    const dialog = page.getByRole("dialog", { name: "Nowa kategoria" })
    await dialog.getByLabel("Nazwa").fill("U12")
    await dialog.getByLabel("Liczba grup").fill("3")
    await expect(dialog.getByText("Powstanie: Grupa A, Grupa B i Grupa C.", { exact: false })).toBeVisible()
    await dialog.getByRole("button", { name: "Zapisz" }).click()
    await expect(dialog).toBeHidden()

    const groups = page.getByRole("region", { name: "Grupy w kategorii U12" })
    await expect(groups.getByRole("listitem")).toHaveText([/Grupa A/, /Grupa B/, /Grupa C/])
  })

  test("[GRP-004] pustą grupę usuwa się przyciskiem przy grupie", async ({ page }) => {
    await groupTournament(page)
    await page.getByRole("button", { name: "Dodaj kategorię" }).first().click()
    const dialog = page.getByRole("dialog", { name: "Nowa kategoria" })
    await dialog.getByLabel("Nazwa").fill("U12")
    await dialog.getByLabel("Liczba grup").fill("2")
    await dialog.getByRole("button", { name: "Zapisz" }).click()
    await expect(dialog).toBeHidden()

    await page.getByRole("button", { name: "Usuń Grupa B" }).click()
    const confirm = page.getByRole("alertdialog", { name: "Usunąć grupę „Grupa B”?" })
    await confirm.getByRole("button", { name: "Usuń" }).click()
    await expect(page.getByRole("button", { name: "Edytuj Grupa B" })).toBeHidden()
    await expect(page.getByRole("button", { name: "Edytuj Grupa A" })).toBeVisible()
  })
})
