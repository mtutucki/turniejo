import { describe, expect, it } from "vitest"

import type { TournamentGroup } from "@/types/tournaments"

import { groupsWithoutPitch, pitchGroupName, pitchGroupsLabel } from "./pitch-groups"

const group = (
  id: string,
  name: string,
  categoryId = "c1",
  order = 0,
  stage?: 1 | 2,
): TournamentGroup => ({
  id,
  tournamentId: "t",
  categoryId,
  name,
  order,
  stage,
})

const oneCategory = [{ id: "c1", name: "U10", order: 0 }]
const twoCategories = [...oneCategory, { id: "c2", name: "U12", order: 1 }]

describe("pitchGroupName", () => {
  it("adds the category only when there are several", () => {
    expect(pitchGroupName(group("a", "Grupa A"), oneCategory)).toBe("Grupa A")
    expect(pitchGroupName(group("a", "Grupa A"), twoCategories)).toBe("U10 · Grupa A")
  })

  it("marks second-stage groups", () => {
    expect(pitchGroupName(group("x", "Grupa I", "c1", 0, 2), oneCategory)).toBe("Grupa I (faza 2)")
  })
})

describe("pitchGroupsLabel", () => {
  const groups = [group("b", "Grupa B", "c1", 1), group("a", "Grupa A", "c1", 0)]

  it("lists assigned groups in order", () => {
    expect(pitchGroupsLabel({ groupIds: ["b", "a"] }, oneCategory, groups)).toBe("Grupa A, Grupa B")
  })

  it("is empty for a pitch open to all groups and ignores deleted groups", () => {
    expect(pitchGroupsLabel({ groupIds: [] }, oneCategory, groups)).toBeNull()
    expect(pitchGroupsLabel({ groupIds: ["gone"] }, oneCategory, groups)).toBeNull()
  })
})

describe("groupsWithoutPitch", () => {
  const groups = [group("a", "Grupa A"), group("b", "Grupa B")]
  const pitches = [
    { id: "p1", groupIds: ["a"] },
    { id: "p2", groupIds: ["b"] },
    { id: "p3", groupIds: [] },
  ]

  it("finds groups whose pitches are not selected", () => {
    expect(groupsWithoutPitch(groups, pitches, ["p1"]).map((g) => g.id)).toEqual(["b"])
  })

  it("a pitch without groups takes every group", () => {
    expect(groupsWithoutPitch(groups, pitches, ["p1", "p3"])).toEqual([])
  })
})
