import { describe, expect, it } from "vitest"

import { countByFilter, filterTeams } from "./team-labels"

const teams = [
  { name: "Orły 2015", shortName: "ORŁ", labelIds: ["y2015", "club"] },
  { name: "Lwy", shortName: null, labelIds: ["y2015"] },
  { name: "Żaki", shortName: "ZAK", labelIds: [] },
]

describe("filterTeams", () => {
  it("etykieta pokazuje drużyny, które ją mają, także z kilkoma etykietami", () => {
    expect(filterTeams(teams, { kind: "label", labelId: "y2015" }, "").map((t) => t.name)).toEqual([
      "Orły 2015",
      "Lwy",
    ])
  })

  it("„Bez etykiety” pokazuje drużyny bez żadnej etykiety", () => {
    expect(filterTeams(teams, { kind: "none" }, "").map((t) => t.name)).toEqual(["Żaki"])
  })

  it("szuka po nazwie i skrócie bez polskich znaków, razem z etykietą", () => {
    expect(filterTeams(teams, { kind: "all" }, "orl").map((t) => t.name)).toEqual(["Orły 2015"])
    expect(filterTeams(teams, { kind: "all" }, "zak").map((t) => t.name)).toEqual(["Żaki"])
    expect(filterTeams(teams, { kind: "label", labelId: "club" }, "lwy")).toEqual([])
  })
})

describe("countByFilter", () => {
  it("liczy drużyny na etykietę i bez etykiety", () => {
    const counts = countByFilter(teams)
    expect(counts.all).toBe(3)
    expect(counts.none).toBe(1)
    expect(counts.byLabel.get("y2015")).toBe(2)
    expect(counts.byLabel.get("club")).toBe(1)
  })
})
