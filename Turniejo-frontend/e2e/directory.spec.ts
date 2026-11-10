import { expect, loginAs, mock, ORGANIZER, PUBLIC_SLUG, test, TOURNAMENT_ID } from "./fixtures"

const day = 24 * 60 * 60 * 1000
const at = (offsetDays: number) => new Date(Date.now() + offsetDays * day).toISOString()

function item(
  name: string,
  organizer: { id: string; name: string },
  fields: Record<string, unknown> = {},
) {
  return {
    name,
    slug: name.toLowerCase().replace(/\s+/g, "-"),
    status: "RegistrationOpen",
    startAt: at(10),
    endAt: at(10),
    organizer,
    categories: ["U10"],
    teamCount: 8,
    liveMatchCount: 0,
    ...fields,
  }
}

const orzel = { id: "org-1", name: "KS Orzeł" }
const iskra = { id: "org-2", name: "UKS Iskra" }

const LIST = [
  item("Puchar Wiosny", orzel, {
    status: "InProgress",
    startAt: at(0),
    endAt: at(1),
    liveMatchCount: 2,
  }),
  item("Liga Orlików", iskra, { status: "ScheduleGenerated", startAt: at(3), endAt: at(3) }),
  item("Halowy Cup", orzel, { startAt: at(30), endAt: at(31), categories: ["U12", "U10"] }),
  ...Array.from({ length: 8 }, (_, i) =>
    item(`Stary Turniej ${i + 1}`, iskra, {
      status: "Finished",
      startAt: at(-40 - i),
      endAt: at(-40 - i),
    }),
  ),
]

test.describe("Lista turniejów publicznych", () => {
  test.beforeEach(async () => {
    await mock.override({ method: "GET", path: "/public/tournaments", body: LIST })
  })

  test("[PUB-020] gość na stronie głównej widzi turnieje w sekcjach wg terminu", async ({
    page,
  }) => {
    await page.goto("/")
    await expect(page.getByRole("heading", { level: 1, name: /Turnieje piłkarskie/ })).toBeVisible()

    const live = page.getByRole("region", { name: /Trwają teraz/ })
    await expect(live.getByRole("article", { name: "Puchar Wiosny" })).toContainText(
      "2 mecze na żywo",
    )
    await expect(live.getByRole("article", { name: "Puchar Wiosny" })).toContainText("KS Orzeł")

    const upcoming = page.getByRole("region", { name: /Nadchodzące/ })
    await expect(upcoming.getByRole("article")).toHaveText([/Liga Orlików/, /Halowy Cup/])
    await expect(upcoming.getByRole("article", { name: "Liga Orlików" })).toContainText("Za 3 dni")

    // Zakończonych jest 8, widać 6 i przycisk po resztę.
    const finished = page.getByRole("region", { name: /Zakończone/ })
    await expect(finished.getByRole("article")).toHaveCount(6)
    await finished.getByRole("button", { name: "Pokaż więcej (2)" }).click()
    await expect(finished.getByRole("article")).toHaveCount(8)

    // Nic nie przewija się w bok na telefonie.
    expect(
      await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth),
    ).toBe(true)
  })

  test("[PUB-021] filtry statusu, terminu, organizatora i kategorii zawężają listę i trafiają do adresu", async ({
    page,
  }) => {
    await page.goto("/t")
    const filters = page.getByRole("search", { name: "Filtry turniejów" })
    const choose = async (label: string, option: RegExp | string) => {
      await filters.getByRole("combobox", { name: label }).click()
      await page.getByRole("option", { name: option }).click()
    }
    const result = page.getByRole("status").filter({ hasText: /turniej/ })
    await expect(result).toHaveText("11 turniejów")

    await choose("Organizator", /KS Orzeł/)
    await expect(page.getByRole("article")).toHaveText([/Puchar Wiosny/, /Halowy Cup/])
    await expect(page).toHaveURL(/org=org-1/)
    await page.getByRole("button", { name: "Usuń filtr KS Orzeł" }).click()

    await choose("Status", /Zakończone/)
    await expect(result).toHaveText("Znaleziono 8 turniejów")
    await expect(page.getByRole("region", { name: /Nadchodzące/ })).toHaveCount(0)
    await choose("Status", "Wszystkie")

    await choose("Termin", "Najbliższe 7 dni")
    await expect(page.getByRole("article")).toHaveText([/Puchar Wiosny/, /Liga Orlików/])
    await choose("Termin", "Wybrane daty")
    await filters.getByLabel("Od").fill(at(20).slice(0, 10))
    await expect(page.getByRole("article")).toHaveText([/Halowy Cup/])
    await choose("Termin", "Dowolny")

    await choose("Kategoria", "U12")
    await expect(page.getByRole("article")).toHaveText([/Halowy Cup/])
    await filters.getByRole("searchbox", { name: /Szukaj/ }).fill("orlikow")
    await expect(page.getByText("Brak turniejów dla tych filtrów")).toBeVisible()
    await page.getByRole("button", { name: "Wyczyść wszystko" }).click()
    await expect(page.getByRole("article")).toHaveCount(9) // 8 zakończonych zwiniętych do 6

    // Organizator na karcie też filtruje; link z filtrem otwiera się od razu przefiltrowany.
    await page
      .getByRole("article", { name: "Liga Orlików" })
      .getByRole("button", { name: "UKS Iskra" })
      .click()
    await expect(page).toHaveURL(/org=org-2/)
    await page.reload()
    await expect(filters.getByRole("combobox", { name: "Organizator" })).toHaveText(/UKS Iskra/)
    await expect(result).toHaveText("Znaleziono 9 turniejów")
  })

  test("[PUB-024] z portalu turnieju kibic wraca do listy", async ({ page }) => {
    await page.goto(`/t/${PUBLIC_SLUG}`)
    await page.getByRole("link", { name: "Wszystkie turnieje" }).click()
    await expect(page).toHaveURL(/\/t$/)
    await expect(page.getByRole("search", { name: "Filtry turniejów" })).toBeVisible()
  })

  test("[PUB-026] zalogowany klika logo na liście turniejów i zostaje na liście", async ({ page }) => {
    await loginAs(page, ORGANIZER)
    await page.goto("/t?status=InProgress")
    await page.getByRole("banner").getByRole("link", { name: "Turniejo" }).click()

    await expect(page).toHaveURL(/\/t$/)
    await expect(page.getByRole("search", { name: "Filtry turniejów" })).toBeVisible()
    // Do panelu prowadzi osobny przycisk.
    await expect(page.getByRole("link", { name: "Mój panel" })).toBeVisible()
  })

  test("[PUB-022] karta prowadzi do portalu turnieju", async ({ page }) => {
    await mock.override({
      method: "GET",
      path: "/public/tournaments",
      body: [item("Puchar E2E", orzel, { slug: PUBLIC_SLUG, status: "InProgress" })],
    })
    await page.goto("/t")
    await page.getByRole("link", { name: "Puchar E2E" }).click()
    await expect(page).toHaveURL(new RegExp(`/t/${PUBLIC_SLUG}$`))
    await expect(page.getByRole("heading", { level: 1, name: "Puchar E2E" })).toBeVisible()
  })
})

test("[PUB-023] organizator ukrywa turniej z listy, a portal dalej działa przez link", async ({
  page,
  browser,
}) => {
  const guest = await browser.newPage()
  await guest.goto("/t")
  await expect(guest.getByRole("article", { name: "Puchar E2E" })).toBeVisible()

  await loginAs(page, ORGANIZER)
  await page.goto(`/tournaments/${TOURNAMENT_ID}/settings#portal`)
  const listed = page.getByRole("switch", { name: "Pokazuj na liście turniejów" })
  await expect(listed).toBeChecked()
  await listed.click()
  await page.getByRole("button", { name: "Zapisz ustawienia portalu" }).click()
  await expect(page.getByText("Portal kibica włączony")).toBeVisible()

  await guest.reload()
  await expect(guest.getByText("Nie ma jeszcze publicznych turniejów")).toBeVisible()
  await guest.goto(`/t/${PUBLIC_SLUG}`)
  await expect(guest.getByRole("heading", { level: 1, name: "Puchar E2E" })).toBeVisible()
  await guest.close()
})
