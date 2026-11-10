import { describe, expect, it } from "vitest"

import type { TournamentTeam } from "@/types/teams"
import type { TournamentGroup } from "@/types/tournaments"

import { autoBracketPairs, bracketSizes, defaultPairCount, suggestPairs } from "./suggest-bracket"

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

describe("bracketSizes", () => {
  it("offers sizes up to 1/8 final at least", () => {
    expect(bracketSizes([], [team("a"), team("b")]).map((s) => s.label)).toEqual([
      "Finał (2 drużyny)",
      "Półfinały (3–4 drużyny)",
      "Ćwierćfinały (5–8 drużyn)",
      "1/8 finału (9–16 drużyn)",
    ])
  })

  it("PUC-020: grows with the number of teams", () => {
    const teams = Array.from({ length: 40 }, (_, i) => team(`t${i}`))
    expect(bracketSizes([], teams).slice(4)).toEqual([
      { pairs: 16, label: "1/16 finału (17–32 drużyny)" },
      { pairs: 32, label: "1/32 finału (33–64 drużyny)" },
    ])
    const many = Array.from({ length: 100 }, (_, i) => team(`t${i}`))
    expect(bracketSizes([], many).at(-1)).toEqual({ pairs: 64, label: "1/64 finału (65–128 drużyn)" })
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

  it("GPK-001: crosses two groups: A1-B2, B1-A2", () => {
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

  it("PUC-008: 8 seeded teams give quarter-finals 1-8, 2-7, 3-6, 4-5 without byes", () => {
    const teams = Array.from({ length: 8 }, (_, i) => team(`t${i + 1}`, null, i + 1))
    expect(defaultPairCount([], teams)).toBe(4)
    const pairs = autoBracketPairs([], teams)!
    const ids = pairs.map((p) =>
      [p.home, p.away].map((slot) => (slot?.kind === "team" ? slot.tournamentTeamId : "bye")).sort().join("-"),
    )
    expect(ids.sort()).toEqual(["t1-t8", "t2-t7", "t3-t6", "t4-t5"])
    expect(pairs.flatMap((p) => [p.home, p.away]).filter((slot) => slot?.kind === "team")).toHaveLength(8)
  })

  it("PUC-015: 5 teams give quarter-finals with three byes for the top seeds", () => {
    const teams = Array.from({ length: 5 }, (_, i) => team(`t${i + 1}`, null, i + 1))
    const pairs = autoBracketPairs([], teams)!
    expect(pairs).toHaveLength(4)
    const byes = pairs.filter((p) => p.away?.kind === "bye" || p.home?.kind === "bye")
    expect(byes).toHaveLength(3)
    expect(byes.map((p) => (p.home?.kind === "team" ? p.home.tournamentTeamId : null)).sort()).toEqual(["t1", "t2", "t3"])
  })

  it("GPK-002: one group of four, semi-finals chosen: 1-4 and 2-3", () => {
    const teams = [team("a1", "A"), team("a2", "A"), team("a3", "A"), team("a4", "A")]
    expect(suggestPairs([group("A", 1)], teams, 2)).toEqual([
      {
        home: { kind: "group", groupId: "A", position: 1 },
        away: { kind: "group", groupId: "A", position: 4 },
      },
      {
        home: { kind: "group", groupId: "A", position: 2 },
        away: { kind: "group", groupId: "A", position: 3 },
      },
    ])
  })

  it("gives up when a group position does not exist", () => {
    const groups = [group("A", 1), group("B", 2), group("C", 3)]
    // 4 pary przy 3 grupach potrzebują 3. i 4. miejsca w grupie A.
    const teams = ["A", "B", "C"].flatMap((g) => [team(`${g}1`, g), team(`${g}2`, g)])
    expect(autoBracketPairs(groups, teams)).toBeNull()
  })

  // PUC-020: drabinka nie ma górnego limitu, 17 drużyn to 1/16 finału z 15 wolnymi losami.
  it("PUC-020: fits every knockout team, also above 16", () => {
    const teams = Array.from({ length: 17 }, (_, i) => team(`t${i}`, null, i + 1))
    const pairs = autoBracketPairs([], teams)!
    expect(pairs).toHaveLength(16)
    expect(pairs.filter((p) => p.away.kind === "bye")).toHaveLength(15)
    expect(pairs[0]).toEqual({ home: { kind: "team", tournamentTeamId: "t0" }, away: { kind: "bye" } })
    expect(pairs[15]).toEqual({
      home: { kind: "team", tournamentTeamId: "t15" },
      away: { kind: "team", tournamentTeamId: "t16" },
    })
    expect(defaultPairCount([], Array.from({ length: 40 }, (_, i) => team(`t${i}`)))).toBe(32)
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
