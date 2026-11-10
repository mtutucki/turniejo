import { describe, expect, it } from "vitest"

import type { TournamentGroup } from "@/types/tournaments"

import {
  assignmentFromSeats,
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
