/** Filtr listy drużyn z bazy: wszystkie, z etykietą albo bez żadnej etykiety. */
export type LabelFilter = { kind: "all" } | { kind: "label"; labelId: string } | { kind: "none" }

export const ALL_TEAMS: LabelFilter = { kind: "all" }

/** Od tylu drużyn w bazie lista podpowiada etykiety (gdy żadnej nie ma) i pokazuje wyszukiwarkę. */
export const MANY_TEAMS = 8

type Labelled = { name: string; shortName?: string | null; labelIds: string[] }

export function matchesLabel(team: Labelled, filter: LabelFilter) {
  if (filter.kind === "all") return true
  if (filter.kind === "none") return team.labelIds.length === 0
  return team.labelIds.includes(filter.labelId)
}

/** Szukanie po nazwie i skrócie, bez wielkości liter i polskich znaków. */
export function matchesQuery(team: Labelled, query: string) {
  const q = normalize(query)
  if (!q) return true
  return normalize(team.name).includes(q) || normalize(team.shortName ?? "").includes(q)
}

export function filterTeams<T extends Labelled>(teams: T[], filter: LabelFilter, query: string) {
  return teams.filter((t) => matchesLabel(t, filter) && matchesQuery(t, query))
}

/** Ile drużyn pokaże każdy filtr (do liczników przy etykietach). */
export function countByFilter(teams: { labelIds: string[] }[]) {
  const byLabel = new Map<string, number>()
  let none = 0
  for (const team of teams) {
    if (team.labelIds.length === 0) none++
    for (const id of team.labelIds) byLabel.set(id, (byLabel.get(id) ?? 0) + 1)
  }
  return { all: teams.length, none, byLabel }
}

function normalize(text: string) {
  return text
    .toLowerCase()
    .replace(/ł/g, "l")
    .normalize("NFD")
    .replace(/\p{Diacritic}/gu, "")
    .trim()
}
