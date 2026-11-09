import { Scenario } from "./api"
import { expect, test, windowFor } from "./fixtures"

test("[GPK-011] grupy + puchar: tabela, drabinka i mistrz zgodne u organizatora i na portalu", async ({ browser }) => {
  const s = await Scenario.groups(2, 2, "GroupAndKnockout")
  const pair = (homeGroup: string, homePos: number, awayGroup: string, awayPos: number) => ({
    homeSourceType: 2, homeGroupId: s.groups.get(homeGroup), homePosition: homePos, homeTournamentTeamId: null,
    awaySourceType: 2, awayGroupId: s.groups.get(awayGroup), awayPosition: awayPos, awayTournamentTeamId: null,
  })
  await s.start()
  await s.organizer.post(`${s.base}/bracket/generate`, {
    categoryId: s.categoryId,
    roundOnePairings: [pair("A", 1, "B", 2), pair("B", 1, "A", 2)],
  })

  // Grupa A: T1 wygrywa z T2, grupa B: T3 wygrywa z T4 → półfinały T1-T4 i T3-T2.
  await s.play("T1", "T2", "T1")
  await s.play("T3", "T4", "T3")
  await s.play("T1", "T4", "T1")
  await s.play("T3", "T2", "T3")
  await s.play("T1", "T3", "T1")

  const fan = await windowFor(browser)
  await fan.goto(`/t/${s.slug}/standings`)
  const groupA = fan.getByRole("region", { name: "Tabela: Grupa A" }).getByRole("listitem")
  await expect(groupA.first()).toContainText("T1")
  await expect(groupA.first()).toContainText(/3$/)
  await expect(fan.getByRole("region", { name: "Tabela: Grupa B" }).getByRole("listitem").first()).toContainText("T3")
  await fan.goto(`/t/${s.slug}`)
  await expect(fan.getByRole("region", { name: "Zwycięzcy" })).toContainText("T1")

  const organizer = await windowFor(browser, s.organizer)
  await organizer.goto(`/tournaments/${s.tournamentId}/bracket`)
  await expect(organizer.getByRole("status").filter({ hasText: "T1" }).first()).toBeVisible()
})
