import { describe, expect, it } from "vitest"

import type { PublicTournamentListItem } from "@/types/public"

import {
  categoriesOf,
  dateRange,
  EMPTY_FILTERS,
  filtersToSearch,
  filterTournaments,
  parseFilters,
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

describe("filterTournaments: status, kategoria i termin", () => {
  const now = new Date("2026-11-11T10:00:00Z") // środa
  const items = [
    item({
      name: "Dziś",
      status: "InProgress",
      startAt: "2026-11-11T08:00:00Z",
      endAt: "2026-11-11T18:00:00Z",
      categories: ["U10"],
    }),
    item({
      name: "Weekend",
      startAt: "2026-11-14T08:00:00Z",
      endAt: "2026-11-15T18:00:00Z",
      categories: ["u12", "U10"],
    }),
    item({ name: "Grudzień", startAt: "2026-12-05T08:00:00Z", endAt: "2026-12-05T18:00:00Z" }),
    item({
      name: "Stary",
      status: "Finished",
      startAt: "2026-10-01T08:00:00Z",
      endAt: "2026-10-01T18:00:00Z",
    }),
  ]
  const names = (f: Partial<typeof EMPTY_FILTERS>) =>
    filterTournaments(items, f, now).map((t) => t.name)

  it("status wybiera jedną sekcję", () => {
    expect(names({ status: "finished" })).toEqual(["Stary"])
    expect(names({ status: "upcoming" })).toEqual(["Weekend", "Grudzień"])
  })

  it("kategoria bez wielkości liter", () => {
    expect(names({ category: "U12" })).toEqual(["Weekend"])
  })

  it("termin: turniej pasuje, gdy któryś jego dzień jest w zakresie", () => {
    expect(names({ when: "today" })).toEqual(["Dziś"])
    expect(names({ when: "weekend" })).toEqual(["Weekend"])
    expect(names({ when: "week" })).toEqual(["Dziś", "Weekend"])
    expect(names({ when: "month" })).toEqual(["Dziś", "Weekend"])
    expect(names({ when: "custom", from: "2026-12-01" })).toEqual(["Grudzień"])
    expect(names({ when: "custom", to: "2026-10-31" })).toEqual(["Stary"])
  })
})

describe("dateRange", () => {
  it("weekend w niedzielę to dzisiejszy dzień, w sobotę sobota i niedziela", () => {
    expect(
      dateRange({ when: "weekend", from: "", to: "" }, new Date("2026-11-15T10:00:00Z")),
    ).toEqual({ from: "2026-11-15", to: "2026-11-15" })
    expect(
      dateRange({ when: "weekend", from: "", to: "" }, new Date("2026-11-14T10:00:00Z")),
    ).toEqual({ from: "2026-11-14", to: "2026-11-15" })
  })

  it("miesiąc od pierwszego do ostatniego dnia", () => {
    expect(
      dateRange({ when: "month", from: "", to: "" }, new Date("2026-02-10T10:00:00Z")),
    ).toEqual({ from: "2026-02-01", to: "2026-02-28" })
  })
})

describe("filtry w adresie", () => {
  it("zapis i odczyt dają te same filtry", () => {
    const filters = {
      ...EMPTY_FILTERS,
      query: "puchar",
      status: "upcoming" as const,
      when: "custom" as const,
      from: "2026-12-01",
      to: "2026-12-31",
      organizerId: "o1",
      category: "U10",
    }
    expect(parseFilters(Object.fromEntries(new URLSearchParams(filtersToSearch(filters))))).toEqual(
      filters,
    )
  })

  it("nieznane wartości są pomijane", () => {
    expect(parseFilters({ status: "zle", when: "jutro", from: "wczoraj" })).toEqual(EMPTY_FILTERS)
    expect(filtersToSearch(EMPTY_FILTERS)).toBe("")
  })
})

it("categoriesOf bez powtórzeń i w kolejności liczb", () => {
  expect(
    categoriesOf([item({ categories: ["U12", "U8"] }), item({ categories: ["u12", "U10"] })]),
  ).toEqual(["U8", "U10", "U12"])
})

describe("organizersOf", () => {
  it("liczy turnieje organizatora i sortuje alfabetycznie", () => {
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
