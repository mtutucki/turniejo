import { expect, loginAs, mock, ORGANIZER, test, TOURNAMENT_ID } from "./fixtures"

type Page = import("@playwright/test").Page

async function newTeam(page: Page, name: string, shortName = "") {
  await page.getByRole("button", { name: "Nowa drużyna" }).click()
  const dialog = page.getByRole("dialog", { name: "Nowa drużyna" })
  await dialog.getByLabel("Nazwa").fill(name)
  if (shortName) await dialog.getByLabel("Skrót (opcjonalnie)").fill(shortName)
  await dialog.getByRole("button", { name: "Zapisz" }).click()
  await expect(dialog).toBeHidden()
}

test.describe("Baza drużyn", () => {
  test.beforeEach(async ({ page }) => {
    await loginAs(page, ORGANIZER)
    await page.goto("/teams")
    await expect(page.getByRole("heading", { name: "Baza drużyn" })).toBeVisible()
  })

  test("[DRU-001] dodanie i edycja drużyny", async ({ page }) => {
    await newTeam(page, "Sokoły", "SOK")
    await expect(page.getByText("Drużyna dodana")).toBeVisible()
    await expect(page.getByRole("link", { name: /Sokoły/ })).toBeVisible()

    await page.getByRole("button", { name: "Edytuj drużynę Sokoły" }).click()
    const dialog = page.getByRole("dialog", { name: "Edytuj drużynę" })
    await dialog.getByLabel("Nazwa").fill("Sokoły Kraków")
    await dialog.getByRole("button", { name: "Zapisz" }).click()

    await expect(page.getByText("Drużyna zapisana")).toBeVisible()
    await expect(page.getByRole("link", { name: /Sokoły Kraków/ })).toBeVisible()
  })

  test("[DRU-002] plik, który nie jest obrazkiem, nie zostaje herbem", async ({ page }) => {
    await page.getByRole("button", { name: "Nowa drużyna" }).click()
    const dialog = page.getByRole("dialog", { name: "Nowa drużyna" })
    await dialog.locator("input[type=file]").setInputFiles({
      name: "herb.png",
      mimeType: "image/png",
      buffer: Buffer.from("to nie jest obrazek"),
    })

    await expect(page.getByText("Nie udało się odczytać obrazka. Wybierz plik PNG, JPG albo WebP.")).toBeVisible()
  })

  test("[DRU-022] HTML i skrypt w nazwie wyświetlają się jako tekst", async ({ page }) => {
    const messages: string[] = []
    page.on("console", (m) => messages.push(m.text()))
    page.on("dialog", (d) => {
      messages.push(`dialog: ${d.message()}`)
      void d.dismiss()
    })
    const name = `<b>Gruby</b><img src=x onerror="console.log('xss')"><script>alert('xss')</script>`

    await newTeam(page, name)

    const link = page.getByRole("link", { name: /Gruby/ })
    await expect(link).toContainText("<b>Gruby</b>")
    await expect(link.locator("b")).toHaveCount(0)
    await expect(link.locator("img, script")).toHaveCount(0)
    await page.reload()
    await expect(page.getByRole("link", { name: /Gruby/ })).toContainText("<script>")
    expect(messages.filter((m) => m.includes("xss"))).toEqual([])
  })
})

test.describe("Zgłoszenia do turnieju", () => {
  test.beforeEach(async ({ page }) => {
    await loginAs(page, ORGANIZER)
  })

  test("[DRU-032] przy jednym wolnym miejscu da się zaznaczyć tylko jedną drużynę", async ({ page }) => {
    await mock.tournament(TOURNAMENT_ID, { teamLimit: 3 })
    await mock.override({
      method: "GET",
      path: "/api/organizations/:id/teams",
      body: [
        { id: "x2", name: "Sokoły", shortName: null, logoUrl: null, organizationId: "o" },
        { id: "x3", name: "Wilki", shortName: null, logoUrl: null, organizationId: "o" },
      ],
    })
    await page.goto(`/tournaments/${TOURNAMENT_ID}/teams`)
    await page.getByRole("button", { name: "Zgłoś drużynę" }).click()
    const dialog = page.getByRole("dialog", { name: "Zgłoś drużyny" })
    await expect(dialog.getByText("Wolne miejsca: 1 z 3")).toBeVisible()

    await dialog.getByRole("checkbox", { name: "Sokoły" }).click()
    await expect(dialog.getByRole("checkbox", { name: "Wilki" })).toBeDisabled()
  })

  test("[DRU-010] kategorii z meczami nie da się usunąć, z wyjaśnieniem", async ({ page }) => {
    await page.goto(`/tournaments/${TOURNAMENT_ID}/categories`)
    await page.getByRole("button", { name: "Usuń kategorię U12" }).click()

    const dialog = page.getByRole("alertdialog", { name: "Kategorii „U12” nie da się jeszcze usunąć" })
    await expect(dialog).toContainText("są rozegrane mecze")
    await dialog.getByRole("button", { name: "Zamknij" }).click()
    await expect(page.getByText("U12").first()).toBeVisible()
  })
})
