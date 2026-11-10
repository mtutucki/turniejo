import { describe, expect, it } from "vitest"

import type { PlannedBracketMatch } from "@/features/matches/planned-bracket-matches"

import { cupPath, outcomeFor, teamRecord } from "./team-record"

let n = 0
const match = (
  home: string,
  away: string,
  score: [number, number] | null,
  opts: { penalties?: [number, number]; status?: string; at?: string } = {},
) => ({
  id: `m${++n}`,
  status: (opts.status ?? (score ? "Finished" : "Scheduled")) as "Finished",
  scheduledAt: opts.at ?? `2026-05-09T${String(8 + n).padStart(2, "0")}:00:00Z`,
  stageType: "Group" as const,
  homeTournamentTeamId: home,
  awayTournamentTeamId: away,
  homeScore: score?.[0] ?? 0,
  awayScore: score?.[1] ?? 0,
  homePenaltyScore: opts.penalties?.[0] ?? null,
  awayPenaltyScore: opts.penalties?.[1] ?? null,
})

describe("outcomeFor", () => {
  it("z perspektywy drużyny, także jako gość", () => {
    expect(outcomeFor(match("A", "B", [2, 1]), "A")).toBe("W")
    expect(outcomeFor(match("A", "B", [2, 1]), "B")).toBe("L")
    expect(outcomeFor(match("A", "B", [1, 1]), "B")).toBe("D")
  })

  it("remis po karnych to zwycięstwo albo porażka, mecz bez wyniku nie ma rezultatu", () => {
    expect(outcomeFor(match("A", "B", [1, 1], { penalties: [3, 4] }), "B")).toBe("W")
    expect(outcomeFor(match("A", "B", null), "A")).toBeNull()
    expect(outcomeFor(match("A", "B", [1, 0], { status: "Live" }), "A")).toBeNull()
  })
})

describe("teamRecord", () => {
  it("bilans, bramki, forma z ostatnich 5 i najbliższy mecz", () => {
    const matches = [
      match("A", "B", [2, 0]),
      match("C", "A", [1, 1]),
      match("A", "D", [0, 3]),
      match("B", "C", [5, 5]),
      match("A", "C", [1, 0], { status: "Live" }),
      match("D", "A", null),
    ]
    const r = teamRecord(matches, "A")

    expect(r.matches).toHaveLength(5)
    expect([r.played, r.wins, r.draws, r.losses]).toEqual([3, 1, 1, 1])
    expect([r.goalsFor, r.goalsAgainst]).toEqual([3, 4])
    expect(r.form).toEqual(["W", "D", "L"])
    expect(r.live.map((m) => m.awayTournamentTeamId)).toEqual(["C"])
    expect(r.next?.homeTournamentTeamId).toBe("D")
  })

  it("forma pokazuje tylko 5 ostatnich wyników", () => {
    const matches = Array.from({ length: 7 }, (_, i) => match("A", "B", i < 2 ? [0, 1] : [1, 0]))
    expect(teamRecord(matches, "A").form).toEqual(["W", "W", "W", "W", "W"])
  })
})

describe("cupPath", () => {
  const planned = (stage: string, teamId: string): PlannedBracketMatch => ({
    nodeId: stage,
    roundId: stage,
    categoryId: "c",
    scheduledAt: "",
    pitchId: null,
    refereeId: null,
    stage,
    home: { label: "A", known: true, tournamentTeamId: teamId },
    away: { label: "Zwycięzca meczu C – D", known: false },
  })

  it("zwycięzca finału", () => {
    const semi = match("A", "B", [2, 1])
    const final = match("C", "A", [0, 0], { penalties: [2, 4] })
    const stages = new Map([
      [semi.id, "Półfinał"],
      [final.id, "Finał"],
    ])
    const path = cupPath([semi, final], "A", stages, [])!
    expect(path.steps.map((s) => s.stage)).toEqual(["Półfinał", "Finał"])
    expect(path.status).toBe("Zwycięzca turnieju")
    expect(path.champion).toBe(true)
  })

  it("odpadnięcie, gra o 3. miejsce i awans do finału", () => {
    const semi = match("A", "B", [0, 1])
    const stages = new Map([[semi.id, "Półfinał"]])
    expect(cupPath([semi], "A", stages, [])!.status).toBe("Odpadła w półfinale")
    expect(cupPath([semi], "A", stages, [planned("Mecz o 3. miejsce", "A")])!.status).toBe(
      "Zagra o 3. miejsce",
    )
    const won = match("A", "B", [3, 1])
    expect(
      cupPath([won], "A", new Map([[won.id, "Półfinał"]]), [planned("Finał", "A")])!.status,
    ).toBe("W finale")
  })

  it("drużyna bez meczów pucharu nie ma drogi w pucharze", () => {
    expect(cupPath([match("A", "B", [1, 0])], "A", new Map(), [])).toBeNull()
  })
})
