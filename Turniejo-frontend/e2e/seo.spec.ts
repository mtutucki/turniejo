import { expect, MATCH_ID, mock, PUBLIC_SLUG, test, TOURNAMENT_ID } from "./fixtures"

type Page = import("@playwright/test").Page

const meta = (page: Page, selector: string) =>
  page.locator(selector).first().getAttribute("content")
/** Dane strukturalne strony: portal ma skrypt turnieju, strona meczu dokłada swój. */
const jsonLd = async (page: Page, type?: (ld: Record<string, unknown>) => boolean) => {
  const all = await page.locator('script[type="application/ld+json"]').allTextContents()
  const parsed = all.map((text) => JSON.parse(text) as Record<string, unknown>)
  return type ? parsed.find(type) : parsed[0]
}

test.describe("SEO", () => {
  // Same znaczniki w <head>: bez różnicy między telefonem a komputerem.
  test.skip(({ isMobile }) => isMobile, "metadane nie zależą od ekranu")

  test("[SEO-001] robots.txt wpuszcza portale i stronę główną, a panel nie", async ({
    request,
  }) => {
    const robots = await (await request.get("/robots.txt")).text()
    expect(robots).toContain("Allow: /")
    expect(robots).toContain("Disallow: /tournaments")
    expect(robots).toContain("Disallow: /login")
    expect(robots).not.toContain("Disallow: /t/")
    expect(robots).toMatch(/Sitemap: https?:\/\/.+\/sitemap\.xml/)
  })

  test("[SEO-002] sitemapa ma turnieje z listy publicznej i ich zakładki", async ({ request }) => {
    const response = await request.get("/sitemap.xml")
    expect(response.ok()).toBe(true)
    const xml = await response.text()
    expect(xml).toContain(`/t/${PUBLIC_SLUG}</loc>`)
    expect(xml).toContain(`/t/${PUBLIC_SLUG}/schedule</loc>`)
    expect(xml).toContain("/help</loc>")
    // Drabinka jest w portalu ukryta (mock: showBracket false), więc nie ma jej też w sitemapie.
    expect(xml).not.toContain(`/t/${PUBLIC_SLUG}/bracket</loc>`)
  })

  test("[SEO-003] portal turnieju: opis, podgląd linku, adres kanoniczny i dane strukturalne", async ({
    page,
    request,
  }) => {
    await page.goto(`/t/${PUBLIC_SLUG}/schedule?team=x`)

    await expect(page).toHaveTitle("Terminarz · Puchar E2E")
    expect(await meta(page, 'meta[name="description"]')).toBe("Turniej testowy E2E")
    expect(await meta(page, 'meta[property="og:title"]')).toBe("Terminarz · Puchar E2E")
    expect(await page.locator('link[rel="canonical"]').getAttribute("href")).toMatch(
      new RegExp(`/t/${PUBLIC_SLUG}/schedule$`),
    )
    expect(await page.locator('meta[name="robots"]').count()).toBe(0)
    const image = await meta(page, 'meta[property="og:image"]')
    expect(image).toContain(`/t/${PUBLIC_SLUG}/opengraph-image`)

    const ld = await jsonLd(page)
    expect(ld).toMatchObject({ "@type": "SportsEvent", name: "Puchar E2E", sport: "Soccer" })

    const png = await request.get(new URL(image!).pathname + new URL(image!).search)
    expect(png.ok()).toBe(true)
    expect(png.headers()["content-type"]).toBe("image/png")
  })

  test("[SEO-004] mecz ma w tytule drużyny i wynik, a podgląd linku pokazuje mecz", async ({
    page,
    request,
  }) => {
    await page.goto(`/t/${PUBLIC_SLUG}/matches/${MATCH_ID}`)

    await expect(page).toHaveTitle("Orły E2E – Lwy E2E 0:0 · Puchar E2E")
    expect(await meta(page, 'meta[name="description"]')).toMatch(/^Na żywo: Orły E2E – Lwy E2E 0:0/)
    const ld = await jsonLd(page, (x) => "homeTeam" in x)
    expect(ld).toMatchObject({
      "@type": "SportsEvent",
      homeTeam: { name: "Orły E2E" },
      awayTeam: { name: "Lwy E2E" },
    })
    const image = await meta(page, 'meta[property="og:image"]')
    expect(image).toContain(`/matches/${MATCH_ID}/opengraph-image`)
    expect((await request.get(new URL(image!).pathname + new URL(image!).search)).ok()).toBe(true)
  })

  test("[SEO-005] turniej ukryty z listy nie trafia do Google, ale ma podgląd linku", async ({
    page,
  }) => {
    await mock.override({
      method: "GET",
      path: `/public/tournaments/${PUBLIC_SLUG}`,
      body: {
        id: TOURNAMENT_ID,
        name: "Puchar E2E",
        slug: PUBLIC_SLUG,
        status: "InProgress",
        format: "Group",
        startAt: "2026-11-10T08:00:00Z",
        endAt: "2026-11-10T16:00:00Z",
        description: null,
        settings: {
          showTeams: true,
          showPlayers: true,
          showSchedule: true,
          showStandings: true,
          showBracket: false,
          showLiveMatches: true,
          showStats: true,
          showInDirectory: false,
        },
        categories: [],
        groups: [],
        pitches: [],
      },
    })
    await page.goto(`/t/${PUBLIC_SLUG}`)

    expect(await meta(page, 'meta[name="robots"]')).toBe("noindex, nofollow")
    expect(await meta(page, 'meta[property="og:image"]')).toContain("/opengraph-image")
  })

  test("[SEO-006] panel i logowanie mają noindex, strona główna nie", async ({ page }) => {
    await page.goto("/login")
    expect(await meta(page, 'meta[name="robots"]')).toBe("noindex, nofollow")

    await page.goto("/")
    expect(await page.locator('meta[name="robots"]').count()).toBe(0)
    expect(await page.locator('link[rel="canonical"]').getAttribute("href")).toMatch(
      /^https?:\/\/[^/]+\/?$/,
    )
    expect((await jsonLd(page))?.["@type"]).toBe("WebSite")
  })
})
