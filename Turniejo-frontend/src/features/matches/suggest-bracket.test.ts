import { describe, expect, it } from "vitest"

import type { TournamentTeam } from "@/types/teams"
import type { TournamentGroup } from "@/types/tournaments"

import { autoBracketPairs, defaultPairCount, suggestPairs } from "./suggest-bracket"

const team = (id: string, groupId: string | null = null, seedNumber: number | null = null) =>
  ({
    id,
    teamName: id,
    groupId,
    seedNumber,
    categoryId: "c",
    registrationStatus: "Accepted",
  }) as TournamentTeam

const group = (id: string, order: number) =>
  ({ id, name: id, order, categoryId: "c", tournamentId: "t" }) as TournamentGroup

describe("defaultPairCount", () => {
  it("rounds up to a power of two", () => {
    expect(defaultPairCount([], [team("a"), team("b"), team("c"), team("d"), team("e")])).toBe(4)
    expect(defaultPairCount([group("A", 1), group("B", 2), group("C", 3)], [])).toBe(4)
    expect(defaultPairCount([group("A", 1)], [])).toBe(1)
  })
})

describe("autoBracketPairs", () => {
  it("seeds a knockout: 1st vs last, top seeds get byes", () => {
    const teams = [team("a", null, 1), team("b", null, 2), team("c", null, 3)]
    expect(autoBracketPairs([], teams)).toEqual([
      { home: { kind: "team", tournamentTeamId: "a" }, away: { kind: "bye" } },
      {
        home: { kind: "team", tournamentTeamId: "b" },
        away: { kind: "team", tournamentTeamId: "c" },
      },
    ])
  })

  it("crosses two groups: A1-B2, B1-A2", () => {
    const groups = [group("A", 1), group("B", 2)]
    const teams = [team("a1", "A"), team("a2", "A"), team("b1", "B"), team("b2", "B")]
    expect(autoBracketPairs(groups, teams)).toEqual([
      {
        home: { kind: "group", groupId: "A", position: 1 },
        away: { kind: "group", groupId: "B", position: 2 },
      },
      {
        home: { kind: "group", groupId: "B", position: 1 },
        away: { kind: "group", groupId: "A", position: 2 },
      },
    ])
  })

  it("gives up when a group position does not exist", () => {
    const groups = [group("A", 1), group("B", 2), group("C", 3)]
    // 4 pary przy 3 grupach potrzebują 3. i 4. miejsca w grupie A.
    const teams = ["A", "B", "C"].flatMap((g) => [team(`${g}1`, g), team(`${g}2`, g)])
    expect(autoBracketPairs(groups, teams)).toBeNull()
  })

  it("gives up when not every knockout team fits", () => {
    const teams = Array.from({ length: 17 }, (_, i) => team(`t${i}`))
    expect(autoBracketPairs([], teams)).toBeNull()
  })

  it("needs at least two teams", () => {
    expect(autoBracketPairs([], [team("a")])).toBeNull()
  })
})

// QA v3: GPK-007 - formularz proponował „4. miejsce” w grupie 3-drużynowej.
describe("suggestPairs", () => {
  it("GPK-007: zostawia do wybrania miejsce, którego w grupie nie ma", () => {
    const teams = [team("a", "A"), team("b", "A"), team("c", "A")]
    const pairs = suggestPairs([group("A", 1)], teams, 2)
    expect(pairs[0]).toEqual({ home: { kind: "group", groupId: "A", position: 1 }, away: null })
    expect(pairs[1]).toEqual({
      home: { kind: "group", groupId: "A", position: 2 },
      away: { kind: "group", groupId: "A", position: 3 },
    })
  })
})
