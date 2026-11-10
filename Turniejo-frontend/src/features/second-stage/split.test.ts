import { describe, expect, it } from "vitest"

import type { TournamentGroup } from "@/types/tournaments"

import {
  assignmentFromSeats,
  effectiveSeats,
  proposeRanges,
  rangeLabel,
  rangesOverlap,
  missingPlaces,
  placesByPosition,
  proposeSplit,
  secondStageMatchCount,
  secondStageProblem,
  seatsFromAssignment,
  suggestSecondStageGroupName,
} from "./split"

const firstStage = ["A", "B", "C", "D"].map((name, i) => ({
  id: name,
  name: `Grupa ${name}`,
  order: i + 1,
  teams: 4,
}))

const group = (id: string, stage: 1 | 2, categoryId = "cat"): TournamentGroup => ({
  id,
  tournamentId: "t",
  categoryId,
  name: `Grupa ${id}`,
  order: 1,
  stage,
})

describe("proposeSplit", () => {
  it("4 grupy po 4 i dwie grupy fazy 2: miejsca 1-2 do pierwszej, 3-4 do drugiej", () => {
    const seats = proposeSplit(firstStage, ["I", "II"])

    expect(
      seats.filter((s) => s.groupId === "I").map((s) => `${s.sourcePosition}${s.sourceGroupId}`),
    ).toEqual(["1A", "1B", "1C", "1D", "2A", "2B", "2C", "2D"])
    expect(
      seats.filter((s) => s.groupId === "II").map((s) => `${s.sourcePosition}${s.sourceGroupId}`),
    ).toEqual(["3A", "3B", "3C", "3D", "4A", "4B", "4C", "4D"])
  })

  it("nieparzysta liczba miejsc: pierwsza grupa dostaje więcej, mniejsza grupa fazy 1 nie daje brakującego miejsca", () => {
    const seats = proposeSplit(
      [
        { id: "A", name: "Grupa A", order: 1, teams: 3 },
        { id: "B", name: "Grupa B", order: 2, teams: 2 },
      ],
      ["I", "II"],
    )

    expect(seats.map((s) => `${s.groupId}:${s.sourcePosition}${s.sourceGroupId}`)).toEqual([
      "I:1A",
      "I:1B",
      "I:2A",
      "I:2B",
      "II:3A",
    ])
  })

  it("bez grup fazy 2 albo bez drużyn nie ma czego dzielić", () => {
    expect(proposeSplit(firstStage, [])).toEqual([])
    expect(proposeSplit([{ id: "A", name: "Grupa A", order: 1, teams: 0 }], ["I"])).toEqual([])
  })
})

describe("seatsFromAssignment", () => {
  it("pomija miejsca bez grupy i układa miejsca po grupie fazy 2, miejscu i grupie fazy 1", () => {
    const assignment = assignmentFromSeats(proposeSplit(firstStage.slice(0, 2), ["I", "II"]))
    assignment["B:4"] = null
    assignment["A:1"] = "II"

    expect(
      seatsFromAssignment(assignment, firstStage.slice(0, 2), ["I", "II"]).map(
        (s) => `${s.groupId}:${s.sourcePosition}${s.sourceGroupId}`,
      ),
    ).toEqual(["I:1B", "I:2A", "I:2B", "II:1A", "II:3A", "II:3B", "II:4A"])
  })
})

describe("secondStageMatchCount", () => {
  it("liczy każdy z każdym w każdej grupie fazy 2", () => {
    expect(secondStageMatchCount(proposeSplit(firstStage, ["I", "II"]))).toBe(28 + 28)
  })
})

describe("suggestSecondStageGroupName", () => {
  it("daje kolejne rzymskie numery", () => {
    expect(suggestSecondStageGroupName([{ name: "Grupa A" }])).toBe("Grupa I")
    expect(suggestSecondStageGroupName([{ name: "Grupa I" }])).toBe("Grupa II")
  })
})

describe("secondStageProblem", () => {
  const groups = [group("A", 1), group("B", 1), group("I", 2), group("II", 2)]
  const teamsInGroup = () => 2

  it("wymaga grupy fazy 2 z podziałem w każdej grającej kategorii", () => {
    expect(
      secondStageProblem({ playingCategoryIds: ["cat"], groups, seats: [], teamsInGroup }),
    ).toMatch(/Każda kategoria/)
  })

  it("grupa fazy 2 z jednym miejscem nie ma meczu", () => {
    const seats = [
      { groupId: "I", sourceGroupId: "A", sourcePosition: 1 },
      { groupId: "I", sourceGroupId: "B", sourcePosition: 1 },
      { groupId: "II", sourceGroupId: "A", sourcePosition: 2 },
    ]
    expect(secondStageProblem({ playingCategoryIds: ["cat"], groups, seats, teamsInGroup })).toBe(
      "Grupa II ma tylko jedno miejsce, a potrzebuje co najmniej dwóch.",
    )
  })

  it("miejsce, którego nie ma w tabeli grupy", () => {
    const seats = [
      { groupId: "I", sourceGroupId: "A", sourcePosition: 1 },
      { groupId: "I", sourceGroupId: "B", sourcePosition: 3 },
    ]
    expect(secondStageProblem({ playingCategoryIds: ["cat"], groups, seats, teamsInGroup })).toBe(
      "W podziale jest 3. miejsce Grupa B, a ta grupa ma mniej drużyn.",
    )
  })

  it("poprawny podział", () => {
    const seats = proposeSplit(
      [
        { id: "A", name: "Grupa A", order: 1, teams: 2 },
        { id: "B", name: "Grupa B", order: 2, teams: 2 },
      ],
      ["I", "II"],
    )
    expect(
      secondStageProblem({ playingCategoryIds: ["cat"], groups, seats, teamsInGroup }),
    ).toBeNull()
  })
})

describe("placesByPosition", () => {
  it("[FAZ-022] układa miejsca wierszami po pozycji, grupy w kolejności", () => {
    const seats = proposeSplit(firstStage.slice(1), ["I", "II"]).filter((s) => s.groupId === "I")
    expect(placesByPosition(seats, firstStage)).toEqual([
      "1. miejsce: Grupa B, Grupa C, Grupa D",
      "2. miejsce: Grupa B, Grupa C, Grupa D",
    ])
  })
})

describe("missingPlaces", () => {
  it("[FAZ-022] wskazuje miejsca, których grupa z drużynami nie ma", () => {
    const seats = [
      { sourceGroupId: "B", sourcePosition: 3 },
      { sourceGroupId: "D", sourcePosition: 3 },
      { sourceGroupId: "D", sourcePosition: 4 },
      { sourceGroupId: "A", sourcePosition: 4 },
    ]
    const teams: Record<string, number> = { A: 0, B: 3, D: 2 }
    expect(missingPlaces(seats, (id) => teams[id] ?? 0, firstStage)).toEqual([
      "3. miejsce Grupa D",
      "4. miejsce Grupa D",
    ])
  })
})

describe("zakresy miejsc", () => {
  it("[FAZ-025] gotowy podział dzieli miejsca po równo na kolejne przedziały", () => {
    expect(proposeRanges(4, ["I", "II"])).toEqual([
      { groupId: "I", fromPosition: 1, toPosition: 2 },
      { groupId: "II", fromPosition: 3, toPosition: 4 },
    ])
    expect(proposeRanges(3, ["I", "II"]).map(rangeLabel)).toEqual(["miejsca 1-2", "miejsce 3"])
    expect(proposeRanges(0, ["I"])).toEqual([])
  })

  it("[FAZ-027] nakładające się zakresy", () => {
    expect(
      rangesOverlap([
        { fromPosition: 1, toPosition: 2 },
        { fromPosition: 3, toPosition: 4 },
      ]),
    ).toBe(false)
    expect(
      rangesOverlap([
        { fromPosition: 1, toPosition: 2 },
        { fromPosition: 2, toPosition: 4 },
      ]),
    ).toBe(true)
  })

  it("[FAZ-026] z zakresów powstają miejsca z prawdziwej liczby drużyn, bez podziału gotowy po równo", () => {
    const groups = [group("A", 1), group("B", 1), group("I", 2), group("II", 2)]
    const teams: Record<string, number> = { A: 3, B: 2 }
    const withRanges = effectiveSeats({
      groups,
      seats: [],
      ranges: [
        { categoryId: "cat", groupId: "I", fromPosition: 1, toPosition: 2 },
        { categoryId: "cat", groupId: "II", fromPosition: 3, toPosition: 4 },
      ],
      teamsInGroup: (id) => teams[id] ?? 0,
    })
    expect(
      withRanges
        .filter((s) => s.groupId === "II")
        .map((s) => `${s.sourcePosition}${s.sourceGroupId}`),
    ).toEqual(["3A"])

    const ready = effectiveSeats({
      groups,
      seats: [],
      ranges: [],
      teamsInGroup: (id) => teams[id] ?? 0,
    })
    expect(ready.map((s) => `${s.groupId}:${s.sourcePosition}${s.sourceGroupId}`)).toEqual([
      "I:1A",
      "I:1B",
      "I:2A",
      "I:2B",
      "II:3A",
    ])
  })
})
