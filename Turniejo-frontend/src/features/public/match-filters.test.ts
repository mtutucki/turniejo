import { describe, expect, it } from "vitest"

import type { PublicMatch } from "@/types/public"

import {
  applyMatchFilters,
  matchFilterOptions,
  matchFiltersToSearch,
  NO_MATCH_FILTERS,
  parseMatchFilters,
} from "./match-filters"

function match(fields: Partial<PublicMatch>): PublicMatch {
  return {
    id: "m",
    tournamentId: "t",
    categoryId: "c1",
    groupId: "g1",
    pitchId: "p1",
    homeTournamentTeamId: "a",
    awayTournamentTeamId: "b",
    refereeId: null,
    scheduledAt: "2026-11-21T09:00:00Z",
    status: "Scheduled",
    stageType: "Group",
    homeScore: 0,
    awayScore: 0,
    resultType: "Regular",
    homePenaltyScore: null,
    awayPenaltyScore: null,
    ...fields,
  } as PublicMatch
}

const tournament = {
  format: "Group" as const,
  categories: [
    { id: "c1", name: "U10", order: 1 },
    { id: "c2", name: "U12", order: 2 },
  ],
  groups: [
    { id: "g1", categoryId: "c1", name: "Grupa A", order: 1 },
    { id: "g2", categoryId: "c2", name: "Grupa A", order: 1 },
  ],
  pitches: [
    { id: "p1", name: "Boisko 1" },
    { id: "p2", name: "Boisko 2" },
  ],
}

describe("applyMatchFilters", () => {
  const matches = [
    match({ id: "1" }),
    match({ id: "2", homeTournamentTeamId: "c", pitchId: "p2", groupId: "g2", categoryId: "c2" }),
    match({ id: "3", awayTournamentTeamId: "c", scheduledAt: "2026-11-22T09:00:00Z" }),
  ]
  const ids = (f: Partial<typeof NO_MATCH_FILTERS>) =>
    applyMatchFilters(matches, { ...NO_MATCH_FILTERS, ...f }).map((m) => m.id)

  it("łączy filtry", () => {
    expect(ids({})).toEqual(["1", "2", "3"])
    expect(ids({ teamId: "c" })).toEqual(["2", "3"])
    expect(ids({ teamId: "c", pitchId: "p1" })).toEqual(["3"])
    expect(ids({ categoryId: "c2" })).toEqual(["2"])
    expect(ids({ day: "2026-11-22" })).toEqual(["3"])
  })
})

describe("matchFilterOptions", () => {
  it("grupy z nazwą kategorii, a po wyborze kategorii tylko jej grupy", () => {
    const all = matchFilterOptions(tournament, [], null)
    expect(all.groups.map((g) => g.label)).toEqual(["U10 · Grupa A", "U12 · Grupa A"])
    expect(matchFilterOptions(tournament, [], "c2").groups).toEqual([
      { id: "g2", label: "Grupa A" },
    ])
  })

  it("bez selectów, które nie mają wyboru", () => {
    const single = matchFilterOptions(
      { ...tournament, categories: [tournament.categories[0]!], pitches: [tournament.pitches[0]!] },
      [match({})],
      null,
    )
    expect(single.categories).toEqual([])
    expect(single.pitches).toEqual([])
    expect(single.days).toEqual([])
    expect(matchFilterOptions({ ...tournament, format: "Knockout" }, [], null).groups).toEqual([])
  })

  it("dni z meczów, po kolei", () => {
    const options = matchFilterOptions(
      tournament,
      [match({ scheduledAt: "2026-11-22T09:00:00Z" }), match({}), match({})],
      null,
    )
    expect(options.days).toEqual(["2026-11-21", "2026-11-22"])
  })
})

it("filtry w adresie", () => {
  const f = { teamId: "a", categoryId: "c1", groupId: "g1", pitchId: "p1", day: "2026-11-21" }
  expect(
    parseMatchFilters(Object.fromEntries(new URLSearchParams(matchFiltersToSearch(f)))),
  ).toEqual(f)
  expect(parseMatchFilters({ day: "jutro" })).toEqual(NO_MATCH_FILTERS)
})
