import { describe, expect, it } from "vitest"

import {
  ALL_TEAMS,
  countByFilter,
  filterTeams,
  groupByLabel,
  NO_LABEL,
  toggleLabel,
} from "./team-labels"

const teams = [
  { name: "Orły 2015", shortName: "ORŁ", labelIds: ["y2015", "club"] },
  { name: "Lwy", shortName: null, labelIds: ["y2015"] },
  { name: "Wilki 2016", shortName: null, labelIds: ["y2016"] },
  { name: "Żaki", shortName: "ZAK", labelIds: [] },
]
const labels = [
  { id: "club", name: "Klub Orlik" },
  { id: "y2015", name: "Rocznik 2015" },
  { id: "y2016", name: "Rocznik 2016" },
]
const names = (list: { name: string }[]) => list.map((t) => t.name)

describe("filterTeams", () => {
  it("bez wybranych etykiet pokazuje wszystkie drużyny", () => {
    expect(filterTeams(teams, ALL_TEAMS, "")).toHaveLength(4)
  })

  it("kilka etykiet to suma: drużyny z którąkolwiek z nich", () => {
    const filter = toggleLabel(toggleLabel(ALL_TEAMS, "y2015"), "y2016")
    expect(names(filterTeams(teams, filter, ""))).toEqual(["Orły 2015", "Lwy", "Wilki 2016"])
  })

  it("„Bez etykiety” łączy się z etykietami", () => {
    expect(names(filterTeams(teams, toggleLabel(ALL_TEAMS, NO_LABEL), ""))).toEqual(["Żaki"])
    const filter = toggleLabel(toggleLabel(ALL_TEAMS, NO_LABEL), "y2016")
    expect(names(filterTeams(teams, filter, ""))).toEqual(["Wilki 2016", "Żaki"])
  })

  it("ponowne kliknięcie etykiety ją wyłącza", () => {
    expect(toggleLabel(toggleLabel(ALL_TEAMS, "y2015"), "y2015")).toEqual(ALL_TEAMS)
  })

  it("szuka po nazwie i skrócie bez polskich znaków, razem z etykietą", () => {
    expect(names(filterTeams(teams, ALL_TEAMS, "orl"))).toEqual(["Orły 2015"])
    expect(names(filterTeams(teams, ALL_TEAMS, "zak"))).toEqual(["Żaki"])
    expect(filterTeams(teams, toggleLabel(ALL_TEAMS, "club"), "lwy")).toEqual([])
  })
})

describe("groupByLabel", () => {
  it("sekcja na etykietę, drużyna w każdej swojej, na końcu „Bez etykiety”", () => {
    const sections = groupByLabel(teams, labels)
    expect(sections.map((s) => [s.title, names(s.teams)])).toEqual([
      ["Klub Orlik", ["Orły 2015"]],
      ["Rocznik 2015", ["Orły 2015", "Lwy"]],
      ["Rocznik 2016", ["Wilki 2016"]],
      ["Bez etykiety", ["Żaki"]],
    ])
  })

  it("przy filtrze tylko wybrane sekcje, puste znikają", () => {
    const filter = toggleLabel(ALL_TEAMS, "y2016")
    expect(groupByLabel(teams, labels, filter).map((s) => s.title)).toEqual(["Rocznik 2016"])
    expect(groupByLabel([teams[3]!], labels).map((s) => s.title)).toEqual(["Bez etykiety"])
  })
})

describe("countByFilter", () => {
  it("liczy drużyny na etykietę i bez etykiety", () => {
    const counts = countByFilter(teams)
    expect(counts.all).toBe(4)
    expect(counts.none).toBe(1)
    expect(counts.byLabel.get("y2015")).toBe(2)
    expect(counts.byLabel.get("club")).toBe(1)
  })
})
