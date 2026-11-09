import { describe, expect, it } from "vitest"

import type { Match } from "@/types/matches"
import type { Bracket, BracketNode } from "@/types/public"

import { bracketChampion, matchWinner, showsChampion } from "./champion-banner"

const match = (overrides: Partial<Match>): Match => ({
  id: "m",
  tournamentId: "t",
  categoryId: "c",
  groupId: null,
  pitchId: null,
  homeTournamentTeamId: "home",
  awayTournamentTeamId: "away",
  refereeId: null,
  scheduledAt: "2026-09-01T10:00:00Z",
  status: "Finished",
  stageType: "Knockout",
  homeScore: 0,
  awayScore: 0,
  resultType: "Regular",
  homePenaltyScore: null,
  awayPenaltyScore: null,
  ...overrides,
})

const node = (id: string, matchId: string, winnerGoesToNodeId: string | null): BracketNode => ({
  id,
  bracketRoundId: "r",
  matchId,
  homeSourceType: "Fixed",
  homeTournamentTeamId: null,
  awaySourceType: "Fixed",
  awayTournamentTeamId: null,
  winnerGoesToNodeId,
  loserGoesToNodeId: null,
  winnerGoesToSlot: null,
  homeSourceGroupId: null,
  homeSourcePosition: null,
  awaySourceGroupId: null,
  awaySourcePosition: null,
  plannedPitchId: null,
  plannedAt: null,
})

// QA v3: PUC-045, PUB-011 - decyzja: baner mistrza zaraz po finale, nie dopiero po „Zakończ turniej”.
describe("showsChampion", () => {
  it("puchar: od razu w trakcie turnieju (baner sam czeka na rozegrany finał)", () => {
    expect(showsChampion("InProgress", "Knockout")).toBe(true)
    expect(showsChampion("InProgress", "GroupAndKnockout")).toBe(true)
  })
  it("same grupy: dopiero po zakończeniu turnieju", () => {
    expect(showsChampion("InProgress", "Group")).toBe(false)
    expect(showsChampion("Finished", "Group")).toBe(true)
  })
  it("przed startem i bez formatu: nigdy", () => {
    expect(showsChampion("ScheduleGenerated", "Knockout")).toBe(false)
    expect(showsChampion("Finished", null)).toBe(false)
  })
})

describe("matchWinner", () => {
  it("returns the team with more goals", () => {
    expect(matchWinner(match({ homeScore: 1, awayScore: 3 }))).toBe("away")
  })

  it("decides a draw on penalties", () => {
    expect(
      matchWinner(match({ homeScore: 2, awayScore: 2, homePenaltyScore: 5, awayPenaltyScore: 4 })),
    ).toBe("home")
  })

  it("returns null for an unfinished match", () => {
    expect(matchWinner(match({ status: "Live", homeScore: 1 }))).toBeNull()
  })
})

describe("bracketChampion", () => {
  const bracket: Bracket = {
    id: "b",
    tournamentId: "t",
    rounds: [
      {
        id: "sf",
        roundNumber: 1,
        roundType: "Standard",
        name: "Półfinał",
        nodes: [node("n1", "semi", "n2")],
      },
      {
        id: "f",
        roundNumber: 2,
        roundType: "Standard",
        name: "Finał",
        nodes: [node("n2", "final", null)],
      },
      {
        id: "3",
        roundNumber: 3,
        roundType: "ThirdPlace",
        name: "O 3. miejsce",
        nodes: [node("n3", "third", null)],
      },
    ],
  }

  it("takes the final's winner, ignoring the third-place match", () => {
    const matches = [
      match({ id: "semi", homeScore: 1 }),
      match({ id: "final", awayScore: 2 }),
      match({ id: "third", homeScore: 4 }),
    ]
    expect(bracketChampion(bracket, matches)).toBe("away")
  })

  it("returns null until the final is played", () => {
    expect(bracketChampion(bracket, [match({ id: "final", status: "Scheduled" })])).toBeNull()
  })
})
