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

/** Nowa etykieta z zakładki „Etykiety” w bazie drużyn. */
async function newLabel(page: Page, name: string) {
  await page.getByRole("button", { name: "Etykiety", exact: true }).click()
  await page.getByRole("button", { name: "Nowa etykieta" }).click()
  const dialog = page.getByRole("dialog", { name: "Nowa etykieta" })
  await dialog.getByLabel("Nazwa").fill(name)
  await dialog.getByRole("button", { name: "Zapisz" }).click()
  await expect(dialog).toBeHidden()
}

const LABELS = [
  { id: "y2015", name: "Rocznik 2015", teamCount: 10 },
  { id: "y2016", name: "Rocznik 2016", teamCount: 8 },
]

const manyTeams = (count: number, labelled: (i: number) => string[] = () => []) =>
  Array.from({ length: count }, (_, i) => ({
    id: `many-${i}`,
    name: `Drużyna ${i + 1}`,
    shortName: null,
    logoUrl: null,
    organizationId: "o",
    labelIds: labelled(i),
  }))

test.describe("Etykiety drużyn", () => {
  test.beforeEach(async ({ page }) => {
    await loginAs(page, ORGANIZER)
    await page.goto("/teams")
    await expect(page.getByRole("heading", { name: "Baza drużyn" })).toBeVisible()
  })

  test("[DRU-048] etykieta dla kilku drużyn naraz i filtr listy drużyn", async ({ page }) => {
    await newLabel(page, "Rocznik 2015")
    await expect(page.getByText("Etykieta dodana")).toBeVisible()

    await page.getByRole("button", { name: "Wybierz drużyny: Rocznik 2015" }).click()
    const members = page.getByRole("dialog", { name: "Drużyny z etykietą Rocznik 2015" })
    await members.getByRole("checkbox", { name: "Orły E2E" }).click()
    await members.getByRole("button", { name: "Zapisz" }).click()
    await expect(members).toBeHidden()
    await expect(page.getByText("1 drużyna")).toBeVisible()

    // Lista dzieli się na sekcje: etykieta i „Bez etykiety”.
    await page.getByRole("button", { name: "Drużyny", exact: true }).click()
    await expect(
      page.getByRole("list", { name: "Rocznik 2015" }).getByRole("link", { name: /Orły E2E/ }),
    ).toBeVisible()
    await expect(
      page.getByRole("list", { name: "Bez etykiety" }).getByRole("link", { name: /Lwy E2E/ }),
    ).toBeVisible()

    const filters = page.getByRole("group", { name: "Etykiety" })
    await filters.getByRole("button", { name: /Rocznik 2015/ }).click()
    await expect(page.getByRole("link", { name: /Orły E2E/ })).toBeVisible()
    await expect(page.getByRole("link", { name: /Lwy E2E/ })).toBeHidden()

    // Drugie kliknięcie wyłącza etykietę, „Bez etykiety” pokazuje resztę.
    await filters.getByRole("button", { name: /Rocznik 2015/ }).click()
    await filters.getByRole("button", { name: /Bez etykiety/ }).click()
    await expect(page.getByRole("link", { name: /Lwy E2E/ })).toBeVisible()
    await expect(page.getByRole("link", { name: /Orły E2E/ })).toBeHidden()
  })

  test("[DRU-055] duża baza w zwiniętych sekcjach etykiet, kilka etykiet naraz w filtrze", async ({
    page,
  }) => {
    await mock.override({ method: "GET", path: "/api/organizations/:id/team-labels", body: LABELS })
    await mock.override({
      method: "GET",
      path: "/api/organizations/:id/teams",
      body: manyTeams(25, (i) => (i < 10 ? ["y2015"] : i < 18 ? ["y2016"] : [])),
    })
    await page.reload()

    // Ponad 20 drużyn: sekcje startują zwinięte, widać tylko nagłówki z licznikami.
    // Nagłówek sekcji (zwija i rozwija), nie przycisk filtra o tej samej nazwie.
    const sectionHeader = (name: string) => page.locator("button[aria-expanded]", { hasText: name })
    const section2015 = sectionHeader("Rocznik 2015")
    await expect(section2015).toContainText("10")
    await expect(section2015).toHaveAttribute("aria-expanded", "false")
    await expect(page.getByRole("link", { name: /Drużyna 1$/ })).toBeHidden()
    await section2015.click()
    await expect(
      page.getByRole("list", { name: "Rocznik 2015" }).getByRole("link", { name: /^Drużyna/ }),
    ).toHaveCount(10)

    // Dwie etykiety naraz: drużyny z którejkolwiek, bez sekcji „Bez etykiety”.
    const filters = page.getByRole("group", { name: "Etykiety" })
    await filters.getByRole("button", { name: /Rocznik 2015/ }).click()
    await filters.getByRole("button", { name: /Rocznik 2016/ }).click()
    await expect(
      page.getByRole("list", { name: "Rocznik 2016" }).getByRole("link", { name: /^Drużyna/ }),
    ).toHaveCount(8)
    await expect(sectionHeader("Bez etykiety")).toHaveCount(0)
    await expect(page.getByRole("list", { name: "Bez etykiety" })).toHaveCount(0)
  })

  test("[DRU-049] etykiety w oknie edycji drużyny, widoczne na liście", async ({ page }) => {
    await newLabel(page, "Klub Orlik")
    await newLabel(page, "Rocznik 2016")
    await page.getByRole("button", { name: "Drużyny", exact: true }).click()
    await newTeam(page, "Sokoły")

    await page.getByRole("button", { name: "Edytuj drużynę Sokoły" }).click()
    const dialog = page.getByRole("dialog", { name: "Edytuj drużynę" })
    await dialog.getByRole("button", { name: "Klub Orlik" }).click()
    await dialog.getByRole("button", { name: "Rocznik 2016" }).click()
    await expect(dialog.getByRole("button", { name: "Klub Orlik" })).toHaveAttribute(
      "aria-pressed",
      "true",
    )
    await dialog.getByRole("button", { name: "Zapisz" }).click()
    await expect(dialog).toBeHidden()

    const card = page
      .getByRole("listitem")
      .filter({ has: page.getByRole("link", { name: /Sokoły/ }) })
    // Drużyna jest w sekcji każdej swojej etykiety.
    await expect(card).toHaveCount(2)
    // W sekcji wiersz pokazuje pozostałe etykiety drużyny, bez etykiety samej sekcji.
    const badgesIn = (section: string) =>
      page.getByRole("list", { name: section }).getByRole("list", { name: "Etykiety drużyny" })
    await expect(badgesIn("Klub Orlik")).toHaveText("Rocznik 2016")
    await expect(badgesIn("Rocznik 2016")).toHaveText("Klub Orlik")
  })

  test("[DRU-050] etykieta o zajętej nazwie nie powstaje, usunięta znika z drużyn", async ({
    page,
  }) => {
    await newLabel(page, "Rocznik 2015")
    await page.getByRole("button", { name: "Nowa etykieta" }).click()
    const dialog = page.getByRole("dialog", { name: "Nowa etykieta" })
    await dialog.getByLabel("Nazwa").fill("rocznik 2015")
    await dialog.getByRole("button", { name: "Zapisz" }).click()
    await expect(dialog.getByText("Etykieta o tej nazwie już istnieje")).toBeVisible()
    await dialog.getByRole("button", { name: "Anuluj" }).click()

    await page.getByRole("button", { name: "Usuń etykietę Rocznik 2015" }).click()
    await page.getByRole("alertdialog").getByRole("button", { name: "Usuń" }).click()
    await expect(page.getByText("Brak etykiet")).toBeVisible()
  })

  test("[DRU-052] przy dużej bazie bez etykiet lista podpowiada etykiety", async ({ page }) => {
    await mock.override({ method: "GET", path: "/api/organizations/:id/teams", body: manyTeams(9) })
    await page.reload()

    await expect(page.getByText("Masz dużo drużyn.")).toBeVisible()
    await expect(page.getByRole("searchbox", { name: "Szukaj drużyny" })).toBeVisible()
    await page.getByRole("link", { name: "Dodaj etykiety" }).click()
    await expect(page.getByText("Brak etykiet")).toBeVisible()
    await expect(page).toHaveURL(/tab=labels/)
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
        { id: "x2", name: "Sokoły", shortName: null, logoUrl: null, organizationId: "o", labelIds: [] },
        { id: "x3", name: "Wilki", shortName: null, logoUrl: null, organizationId: "o", labelIds: [] },
      ],
    })
    await page.goto(`/tournaments/${TOURNAMENT_ID}/teams`)
    await page.getByRole("button", { name: "Zgłoś drużynę" }).click()
    const dialog = page.getByRole("dialog", { name: "Zgłoś drużyny" })
    await expect(dialog.getByText("Wolne miejsca: 1 z 3")).toBeVisible()

    await dialog.getByRole("checkbox", { name: "Sokoły" }).click()
    await expect(dialog.getByRole("checkbox", { name: "Wilki" })).toBeDisabled()
  })

  test("[DRU-053] w zgłoszeniu drużyny są w sekcjach etykiet, a etykietę zaznacza się jednym polem", async ({
    page,
  }) => {
    await mock.override({ method: "GET", path: "/api/organizations/:id/team-labels", body: LABELS })
    await mock.override({
      method: "GET",
      path: "/api/organizations/:id/teams",
      body: manyTeams(10, (i) => (i < 3 ? ["y2015"] : i < 5 ? ["y2016"] : [])),
    })
    await page.goto(`/tournaments/${TOURNAMENT_ID}/teams`)
    await page.getByRole("button", { name: "Zgłoś drużynę" }).click()
    const dialog = page.getByRole("dialog", { name: "Zgłoś drużyny" })

    await expect(
      dialog.getByRole("list", { name: "Rocznik 2015" }).getByRole("checkbox"),
    ).toHaveCount(3)
    await dialog.getByRole("checkbox", { name: "Zaznacz etykietę Rocznik 2015" }).click()

    await expect(dialog.getByText(/Zaznaczono 3\./)).toBeVisible()
    await expect(dialog.getByRole("button", { name: "Zgłoś 3 drużyny" })).toBeEnabled()
    // Drugie kliknięcie odznacza całą etykietę.
    await dialog.getByRole("checkbox", { name: "Zaznacz etykietę Rocznik 2015" }).click()
    await expect(dialog.getByText(/Zaznaczono 0\./)).toBeVisible()
  })

  test("[DRU-054] w zgłoszeniu kilka etykiet naraz i „Zaznacz wszystkie” wybiera drużyny z nich", async ({
    page,
  }) => {
    await mock.override({ method: "GET", path: "/api/organizations/:id/team-labels", body: LABELS })
    await mock.override({
      method: "GET",
      path: "/api/organizations/:id/teams",
      body: manyTeams(10, (i) => (i < 3 ? ["y2015"] : i < 5 ? ["y2016"] : [])),
    })
    await page.goto(`/tournaments/${TOURNAMENT_ID}/teams`)
    await page.getByRole("button", { name: "Zgłoś drużynę" }).click()
    const dialog = page.getByRole("dialog", { name: "Zgłoś drużyny" })

    const filters = dialog.getByRole("group", { name: "Etykiety" })
    await filters.getByRole("button", { name: /Rocznik 2015/ }).click()
    await filters.getByRole("button", { name: /Rocznik 2016/ }).click()
    await expect(dialog.getByRole("list", { name: "Bez etykiety" })).toHaveCount(0)
    await dialog.getByRole("button", { name: "Zaznacz wszystkie" }).click()

    await expect(dialog.getByText(/Zaznaczono 5\./)).toBeVisible()
    await expect(dialog.getByRole("button", { name: "Zgłoś 5 drużyn" })).toBeEnabled()
  })

  test("[DRU-052] zgłoszenie przy dużej bazie bez etykiet podpowiada etykiety", async ({
    page,
  }) => {
    await mock.override({ method: "GET", path: "/api/organizations/:id/teams", body: manyTeams(9) })
    await page.goto(`/tournaments/${TOURNAMENT_ID}/teams`)
    await page.getByRole("button", { name: "Zgłoś drużynę" }).click()
    const dialog = page.getByRole("dialog", { name: "Zgłoś drużyny" })

    await expect(dialog.getByText("Masz dużo drużyn.", { exact: false })).toBeVisible()
    await expect(dialog.getByRole("link", { name: "Dodaj etykiety" })).toBeVisible()
  })

  test("[DRU-037] akceptacja wszystkich oczekujących jednym przyciskiem", async ({ page }) => {
    await mock.tournament(TOURNAMENT_ID, { status: "RegistrationOpen" })
    await pendingTeams(3)
    await page.goto(`/tournaments/${TOURNAMENT_ID}/teams`)
    await expect(page.getByText("3 drużyny czekają na akceptację.", { exact: false })).toBeVisible()

    await page.getByRole("button", { name: "Akceptuj wszystkie" }).click()

    await expect(page.getByText("Zaakceptowano 3 drużyny")).toBeVisible()
    expect(await mock.calls("PATCH", "/registration-status")).toBe(3)
  })

  test("[DRU-038] przy małym limicie akceptuje tyle drużyn, ile się zmieści", async ({ page }) => {
    await mock.tournament(TOURNAMENT_ID, { teamLimit: 2, status: "RegistrationOpen" })
    await pendingTeams(3)
    await page.goto(`/tournaments/${TOURNAMENT_ID}/teams`)
    await expect(page.getByText("W limicie zmieści się jeszcze 2 drużyny.")).toBeVisible()

    await page.getByRole("button", { name: "Akceptuj 2 drużyny" }).click()

    await expect(page.getByText("Zaakceptowano 2 drużyny")).toBeVisible()
    expect(await mock.calls("PATCH", "/registration-status")).toBe(2)
  })

  test("[GRP-030] drużyny pokazane w ramkach swoich grup, przeniesienie zmienia ramkę", async ({ page }) => {
    // Przed terminarzem: po jego wygenerowaniu grup nie da się już zmienić (GRP-031).
    await mock.tournament(TOURNAMENT_ID, { status: "RegistrationClosed" })
    const group = (id: string, name: string, order: number) => ({
      id,
      tournamentId: TOURNAMENT_ID,
      categoryId: "0f000000-0000-4000-8000-000000000020",
      name,
      order,
    })
    await mock.override({
      method: "GET",
      path: "/api/tournaments/:id/groups",
      body: [
        group("0f000000-0000-4000-8000-000000000030", "Grupa A", 1),
        group("0f000000-0000-4000-8000-000000000031", "Grupa B", 2),
      ],
    })
    await page.goto(`/tournaments/${TOURNAMENT_ID}/teams`)
    const groupA = page.getByRole("region", { name: "Grupa A" })
    const groupB = page.getByRole("region", { name: "Grupa B" })
    await expect(groupA.getByRole("link", { name: /Orły E2E/ }).first()).toBeVisible()
    await expect(groupA.getByRole("link", { name: /Lwy E2E/ }).first()).toBeVisible()
    await expect(groupB.getByText("Brak drużyn w tej grupie.")).toBeVisible()

    await groupA.getByRole("combobox", { name: "Grupa: Lwy E2E" }).click()
    await page.getByRole("option", { name: "Grupa B" }).click()
    await expect(groupB.getByRole("link", { name: /Lwy E2E/ }).first()).toBeVisible()
    await expect(groupA.getByRole("link", { name: /Lwy E2E/ })).toHaveCount(0)

    await groupB.getByRole("combobox", { name: "Grupa: Lwy E2E" }).click()
    await page.getByRole("option", { name: "Bez grupy" }).click()
    const none = page.getByRole("region", { name: "Bez grupy" })
    await expect(none.getByText("Te drużyny nie zagrają, dopóki nie trafią do grupy.")).toBeVisible()
    await expect(none.getByRole("link", { name: /Lwy E2E/ }).first()).toBeVisible()
  })

  test("[GRP-031 GRP-032] po wygenerowaniu terminarza grup i listy drużyn nie da się zmienić", async ({ page }) => {
    const category = "0f000000-0000-4000-8000-000000000020"
    await mock.tournament(TOURNAMENT_ID, { status: "ScheduleGenerated" })
    await mock.override({
      method: "GET",
      path: "/api/tournaments/:id/groups",
      body: ["Grupa A", "Grupa B"].map((name, i) => ({
        id: `0f000000-0000-4000-8000-00000000003${i}`,
        tournamentId: TOURNAMENT_ID,
        categoryId: category,
        name,
        order: i + 1,
      })),
    })
    await pendingTeams(2)
    await page.goto(`/tournaments/${TOURNAMENT_ID}/teams`)

    await expect(page.getByText("Terminarz jest gotowy, więc grup i listy drużyn nie zmienisz.", { exact: false })).toBeVisible()
    await expect(page.getByRole("combobox", { name: "Grupa: Oczekująca 1" })).toBeDisabled()
    await expect(page.getByRole("button", { name: "Akceptuj", exact: true }).first()).toBeDisabled()
    await expect(page.getByRole("button", { name: "Akceptuj wszystkie" })).toHaveCount(0)
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
