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

/** Zgłoszenia turnieju: n oczekujących drużyn „Oczekująca 1…n” w kategorii U12 z seeda. */
async function pendingTeams(n: number) {
  await mock.override({
    method: "GET",
    path: "/api/tournaments/:id/teams",
    body: Array.from({ length: n }, (_, i) => ({
      id: `0f000000-0000-4000-8000-0000000002${String(i).padStart(2, "0")}`,
      tournamentId: TOURNAMENT_ID,
      teamId: `p${i}`,
      categoryId: "0f000000-0000-4000-8000-000000000020",
      groupId: null,
      registrationStatus: "Pending",
      paymentStatus: "NotRequired",
      seedNumber: null,
      teamName: `Oczekująca ${i + 1}`,
      teamShortName: null,
      teamLogoUrl: null,
    })),
  })
}

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

  test("[DRU-037] akceptacja wszystkich oczekujących jednym przyciskiem", async ({ page }) => {
    await pendingTeams(3)
    await page.goto(`/tournaments/${TOURNAMENT_ID}/teams`)
    await expect(page.getByText("3 drużyny czekają na akceptację.", { exact: false })).toBeVisible()

    await page.getByRole("button", { name: "Akceptuj wszystkie" }).click()

    await expect(page.getByText("Zaakceptowano 3 drużyny")).toBeVisible()
    expect(await mock.calls("PATCH", "/registration-status")).toBe(3)
  })

  test("[DRU-038] przy małym limicie akceptuje tyle drużyn, ile się zmieści", async ({ page }) => {
    await mock.tournament(TOURNAMENT_ID, { teamLimit: 2 })
    await pendingTeams(3)
    await page.goto(`/tournaments/${TOURNAMENT_ID}/teams`)
    await expect(page.getByText("W limicie zmieści się jeszcze 2 drużyny.")).toBeVisible()

    await page.getByRole("button", { name: "Akceptuj 2 drużyny" }).click()

    await expect(page.getByText("Zaakceptowano 2 drużyny")).toBeVisible()
    expect(await mock.calls("PATCH", "/registration-status")).toBe(2)
  })

  test("[DRU-010] kategorii z meczami nie da się usunąć, z wyjaśnieniem", async ({ page }) => {
    await page.goto(`/tournaments/${TOURNAMENT_ID}/categories`)
    await page.getByRole("button", { name: "Usuń kategorię U12" }).click()

    const dialog = page.getByRole("alertdialog", { name: "Kategorii „U12” nie da się jeszcze usunąć" })
    await expect(dialog).toContainText("są rozegrane mecze")
    await dialog.getByRole("button", { name: "Zamknij" }).click()
    await expect(page.getByText("U12").first()).toBeVisible()
  })

  test("[GRP-004] grupy z drużynami nie da się usunąć, z wyjaśnieniem", async ({ page }) => {
    await page.goto(`/tournaments/${TOURNAMENT_ID}/categories`)
    await page.getByRole("button", { name: "Usuń Grupa A" }).click()

    const dialog = page.getByRole("alertdialog", { name: "Grupy „Grupa A” nie da się jeszcze usunąć" })
    await expect(dialog.getByRole("link", { name: "Przejdź do Drużyn" })).toBeVisible()
    await expect(dialog.getByRole("link", { name: "Przejdź do Terminarza" })).toBeVisible()
    await expect(dialog.getByRole("button", { name: "Usuń" })).toHaveCount(0)
  })

  test("[GRP-002] po wygenerowaniu terminarza nowa kategoria nie pyta o liczbę grup", async ({ page }) => {
    await page.goto(`/tournaments/${TOURNAMENT_ID}/categories`)
    await page.getByRole("button", { name: "Dodaj kategorię" }).first().click()
    const dialog = page.getByRole("dialog", { name: "Nowa kategoria" })
    await expect(dialog.getByLabel("Nazwa")).toBeVisible()
    await expect(dialog.getByLabel("Liczba grup")).toHaveCount(0)
  })
})
