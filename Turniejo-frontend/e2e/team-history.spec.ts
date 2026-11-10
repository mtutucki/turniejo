import { expect, loginAs, MATCH_ID, mock, ORGANIZER, test, TOURNAMENT_ID } from "./fixtures"

type Page = import("@playwright/test").Page

const ORGANIZATION_ID = "0f000000-0000-4000-8000-000000000001"

const noHorizontalScroll = async (page: Page) =>
  expect
    .poll(() => page.evaluate(() => document.documentElement.scrollWidth - window.innerWidth))
    .toBeLessThanOrEqual(0)

test("[DRU-046] drużyna w bazie: statystyki ze wszystkich turniejów i link do turnieju", async ({
  page,
}) => {
  await mock.matchStatus(MATCH_ID, "Finished")
  await loginAs(page, ORGANIZER)
  await page.goto(`/organizations/${ORGANIZATION_ID}/teams/team-orly`)

  await expect(page.getByText("Statystyki we wszystkich turniejach")).toBeVisible()
  const summary = page.getByRole("region", { name: "Bilans we wszystkich turniejach" })
  await expect(summary).toContainText("0-1-0")
  await expect(summary).toContainText("0%")
  await expect(page.getByLabel("Forma").getByLabel("Remis")).toBeVisible()

  const tournament = page.getByTestId("history-tournament")
  await expect(tournament).toHaveCount(1)
  await expect(tournament).toContainText("1. miejsce w Grupie A")
  await expect(page.getByTestId("history-opponent")).toContainText("Lwy E2E")

  await tournament.getByRole("link", { name: "Puchar E2E" }).click()
  await expect(page).toHaveURL(new RegExp(`/tournaments/${TOURNAMENT_ID}/teams/`))
  await expect(page.getByText("Mecze i wyniki")).toBeVisible()
})

test("[DRU-047] drużyna bez turniejów mówi to wprost, kadra zostaje", async ({ page }) => {
  await mock.override({
    method: "GET",
    path: "/api/teams/:teamId/history",
    body: {
      summary: { played: 0, wins: 0, draws: 0, losses: 0, goalsFor: 0, goalsAgainst: 0 },
      tournamentCount: 0,
      form: [],
      tournaments: [],
      players: [],
      opponents: [],
    },
  })
  await loginAs(page, ORGANIZER)
  await page.goto(`/organizations/${ORGANIZATION_ID}/teams/team-orly`)

  await expect(page.getByText("Drużyna nie grała jeszcze w żadnym turnieju.")).toBeVisible()
  await expect(page.getByTestId("history-tournament")).toHaveCount(0)
  await noHorizontalScroll(page)
})
