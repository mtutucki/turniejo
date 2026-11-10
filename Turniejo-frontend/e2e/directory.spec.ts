import { expect, mock, PUBLIC_SLUG, test } from "./fixtures"

const day = 24 * 60 * 60 * 1000
const at = (offsetDays: number) => new Date(Date.now() + offsetDays * day).toISOString()

function item(name: string, organizer: { id: string; name: string }, fields: Record<string, unknown> = {}) {
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
  item("Puchar Wiosny", orzel, { status: "InProgress", startAt: at(0), endAt: at(1), liveMatchCount: 2 }),
  item("Liga Orlików", iskra, { status: "ScheduleGenerated", startAt: at(3), endAt: at(3) }),
  item("Halowy Cup", orzel, { startAt: at(30), endAt: at(31) }),
  ...Array.from({ length: 8 }, (_, i) =>
    item(`Stary Turniej ${i + 1}`, iskra, { status: "Finished", startAt: at(-40 - i), endAt: at(-40 - i) }),
  ),
]

test.describe("Lista turniejów publicznych", () => {
  test.beforeEach(async () => {
    await mock.override({ method: "GET", path: "/public/tournaments", body: LIST })
  })

  test("[PUB-020] gość na stronie głównej widzi turnieje w sekcjach wg terminu", async ({ page }) => {
    await page.goto("/")
    await expect(page.getByRole("heading", { level: 1, name: /Turnieje piłkarskie/ })).toBeVisible()

    const live = page.getByRole("region", { name: /Trwają teraz/ })
    await expect(live.getByRole("article", { name: "Puchar Wiosny" })).toContainText("2 mecze na żywo")
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
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true)
  })

  test("[PUB-021] filtr organizatora i wyszukiwarka zawężają listę, a filtr trafia do adresu", async ({ page }) => {
    await page.goto("/t")
    const organizers = page.getByRole("group", { name: "Organizator" })

    await organizers.getByRole("button", { name: /KS Orzeł/ }).click()
    await expect(page.getByRole("article")).toHaveCount(2)
    await expect(page).toHaveURL(/org=org-1/)

    await organizers.getByRole("button", { name: /Wszyscy/ }).click()
    await page.getByRole("searchbox", { name: /Szukaj/ }).fill("orlikow")
    await expect(page.getByRole("article")).toHaveText([/Liga Orlików/])

    await page.getByRole("searchbox", { name: /Szukaj/ }).fill("nie ma takiego")
    await page.getByRole("button", { name: "Wyczyść filtry" }).click()
    await expect(page.getByRole("article")).toHaveCount(9)

    // Organizator na karcie też filtruje; link z filtrem otwiera się od razu przefiltrowany.
    await page.getByRole("article", { name: "Liga Orlików" }).getByRole("button", { name: "UKS Iskra" }).click()
    await expect(page).toHaveURL(/org=org-2/)
    await page.reload()
    await expect(page.getByRole("article")).toHaveCount(7)
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
