import { describe, expect, it } from "vitest"

import type { TournamentGroup } from "@/types/tournaments"

import { groupSections } from "./group-sections"

const group = (id: string, order: number): TournamentGroup => ({
  id,
  tournamentId: "t",
  categoryId: "c",
  name: `Grupa ${id.toUpperCase()}`,
  order,
})
const team = (name: string, groupId: string | null) => ({ name, groupId })

describe("groupSections", () => {
  it("dzieli drużyny na grupy w kolejności grup, z pustymi grupami", () => {
    const sections = groupSections(
      [team("1", "b"), team("2", "a"), team("3", "b")],
      [group("c", 3), group("b", 2), group("a", 1)],
    )

    expect(sections?.map((s) => [s.group?.id, s.teams.map((t) => t.name)])).toEqual([
      ["a", ["2"]],
      ["b", ["1", "3"]],
      ["c", []],
    ])
  })

  it("drużyny bez grupy albo z usuniętą grupą idą na początek", () => {
    const sections = groupSections(
      [team("1", "a"), team("2", null), team("3", "gone")],
      [group("a", 1), group("b", 2)],
    )

    expect(sections?.[0]).toEqual({ group: null, teams: [team("2", null), team("3", "gone")] })
  })

  it("bez grup albo z jedną pełną grupą nie dzieli listy", () => {
    expect(groupSections([team("1", null)], [])).toBeNull()
    expect(groupSections([team("1", "a")], [group("a", 1)])).toBeNull()
  })

  it("jedna grupa i drużyna bez grupy: dzieli, żeby było ją widać", () => {
    const sections = groupSections([team("1", "a"), team("2", null)], [group("a", 1)])

    expect(sections?.map((s) => s.group?.id ?? null)).toEqual([null, "a"])
  })
})
