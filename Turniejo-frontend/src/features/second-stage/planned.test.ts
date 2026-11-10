import { describe, expect, it } from "vitest"

import type { SecondStage } from "@/types/tournaments"

import { plannedSecondStageMatches } from "./planned"

const seat = (
  id: string,
  sourceGroupId: string,
  sourcePosition: number,
  tournamentTeamId: string | null = null,
) => ({
  id,
  categoryId: "cat",
  groupId: "I",
  order: 1,
  sourceGroupId,
  sourcePosition,
  tournamentTeamId,
})

const fixture = (
  id: string,
  home: string,
  away: string,
  plannedAt: string,
  matchId: string | null = null,
) => ({
  id,
  categoryId: "cat",
  groupId: "I",
  homeSeatId: home,
  awaySeatId: away,
  round: 1,
  plannedPitchId: "p1",
  plannedAt,
  matchId,
})

const groups = [
  { id: "A", name: "Grupa A" },
  { id: "B", name: "Grupa B" },
  { id: "I", name: "Grupa I" },
]

describe("plannedSecondStageMatches", () => {
  it("zapowiada mecze bez drużyn opisem miejsca, a znane drużyny nazwą", () => {
    const stage: SecondStage = {
      seats: [seat("s1", "A", 1, "t1"), seat("s2", "B", 2)],
      fixtures: [fixture("f1", "s1", "s2", "2026-11-14T12:00:00Z")],
    }

    const [planned] = plannedSecondStageMatches(stage, groups, (id) => (id === "t1" ? "Orły" : "?"))

    expect(planned).toMatchObject({
      nodeId: "f1",
      stage: "Grupa I",
      pitchId: "p1",
      secondStageGroupId: "I",
      home: { label: "Orły", known: true, tournamentTeamId: "t1" },
      away: { label: "2. miejsce Grupa B", known: false },
    })
  })

  it("pomija mecze, które już powstały, i układa resztę po godzinie", () => {
    const stage: SecondStage = {
      seats: [seat("s1", "A", 1), seat("s2", "B", 1), seat("s3", "A", 2)],
      fixtures: [
        fixture("late", "s1", "s2", "2026-11-14T14:00:00Z"),
        fixture("done", "s1", "s3", "2026-11-14T12:00:00Z", "m1"),
        fixture("early", "s2", "s3", "2026-11-14T13:00:00Z"),
      ],
    }

    expect(plannedSecondStageMatches(stage, groups, () => "").map((p) => p.nodeId)).toEqual([
      "early",
      "late",
    ])
  })
})
