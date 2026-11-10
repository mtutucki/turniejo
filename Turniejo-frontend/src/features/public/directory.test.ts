import { describe, expect, it } from "vitest"

import type { PublicTournamentListItem } from "@/types/public"

import {
  filterTournaments,
  groupTournaments,
  normalize,
  organizersOf,
  startsInLabel,
} from "./directory"

const orzel = { id: "o1", name: "KS Orzeł Łódź" }
const iskra = { id: "o2", name: "UKS Iskra" }

function item(fields: Partial<PublicTournamentListItem>): PublicTournamentListItem {
  return {
    name: "Turniej",
    slug: fields.name ?? "turniej",
    status: "RegistrationOpen",
    startAt: "2026-12-01T09:00:00Z",
    endAt: "2026-12-01T17:00:00Z",
    organizer: orzel,
    categories: [],
    teamCount: 0,
    liveMatchCount: 0,
    ...fields,
  }
}

describe("groupTournaments", () => {
  it("dzieli na trwające, nadchodzące i zakończone, w kolejności terminu", () => {
    const items = [
      item({ name: "Później", startAt: "2026-12-20T09:00:00Z" }),
      item({ name: "Wcześniej", startAt: "2026-12-05T09:00:00Z", status: "ScheduleGenerated" }),
      item({ name: "Trwa", status: "InProgress" }),
      item({ name: "Na żywo", status: "InProgress", liveMatchCount: 2 }),
      item({ name: "Stary", status: "Finished", endAt: "2026-05-01T17:00:00Z" }),
      item({ name: "Świeży", status: "Finished", endAt: "2026-11-01T17:00:00Z" }),
    ]

    const groups = groupTournaments(items)

    expect(groups.live.map((t) => t.name)).toEqual(["Na żywo", "Trwa"])
    expect(groups.upcoming.map((t) => t.name)).toEqual(["Wcześniej", "Później"])
    expect(groups.finished.map((t) => t.name)).toEqual(["Świeży", "Stary"])
  })
})

describe("filterTournaments", () => {
  const items = [
    item({ name: "Puchar Wiosny", organizer: orzel }),
    item({ name: "Liga Orlików", organizer: iskra }),
  ]

  it("szuka w nazwie turnieju i organizatora, bez polskich znaków", () => {
    expect(filterTournaments(items, { query: "orlik", organizerId: null })).toHaveLength(1)
    expect(
      filterTournaments(items, { query: "lodz", organizerId: null }).map((t) => t.name),
    ).toEqual(["Puchar Wiosny"])
  })

  it("filtruje po organizatorze", () => {
    expect(filterTournaments(items, { query: "", organizerId: "o2" }).map((t) => t.name)).toEqual([
      "Liga Orlików",
    ])
  })
})

describe("organizersOf", () => {
  it("liczy turnieje organizatora i sortuje od największej liczby", () => {
    const organizers = organizersOf([
      item({ organizer: iskra }),
      item({ organizer: orzel }),
      item({ organizer: orzel }),
    ])
    expect(organizers).toEqual([
      { ...orzel, count: 2 },
      { ...iskra, count: 1 },
    ])
  })
})

describe("startsInLabel", () => {
  const now = new Date("2026-11-10T10:00:00Z")
  it.each([
    ["2026-11-10T16:00:00Z", "Dziś"],
    ["2026-11-11T08:00:00Z", "Jutro"],
    ["2026-11-15T08:00:00Z", "Za 5 dni"],
    ["2026-12-30T08:00:00Z", null],
  ])("%s → %s", (startAt, label) => {
    expect(startsInLabel(startAt, now)).toBe(label)
  })
})

it("normalize ignoruje wielkość liter i polskie znaki", () => {
  expect(normalize("  Łódź ŻÓŁW ")).toBe("lodz zolw")
})
