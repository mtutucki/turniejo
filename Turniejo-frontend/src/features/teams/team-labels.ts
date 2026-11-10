/**
 * Filtr listy drużyn z bazy: kilka etykiet naraz i/lub „Bez etykiety”. Drużyna pasuje, gdy ma
 * którąkolwiek z wybranych etykiet (suma, nie część wspólna). Nic nie wybrane: wszystkie drużyny.
 */
export type LabelFilter = { labelIds: string[]; none: boolean }

export const ALL_TEAMS: LabelFilter = { labelIds: [], none: false }

/** Specjalny klucz sekcji i przełącznika „Bez etykiety”. */
export const NO_LABEL = "__none__"

/** Od tylu drużyn w bazie lista podpowiada etykiety (gdy żadnej nie ma) i pokazuje wyszukiwarkę. */
export const MANY_TEAMS = 8

type Labelled = { name: string; shortName?: string | null; labelIds: string[] }

export const isAllTeams = (filter: LabelFilter) => filter.labelIds.length === 0 && !filter.none

/** Włącza albo wyłącza etykietę (albo NO_LABEL) w filtrze. */
export function toggleLabel(filter: LabelFilter, key: string): LabelFilter {
  if (key === NO_LABEL) return { ...filter, none: !filter.none }
  return filter.labelIds.includes(key)
    ? { ...filter, labelIds: filter.labelIds.filter((id) => id !== key) }
    : { ...filter, labelIds: [...filter.labelIds, key] }
}

/** Usuwa z filtra etykiety, których już nie ma (np. usunięte w innej karcie). */
export function pruneFilter(filter: LabelFilter, existing: { id: string }[]): LabelFilter {
  const ids = filter.labelIds.filter((id) => existing.some((l) => l.id === id))
  return ids.length === filter.labelIds.length ? filter : { ...filter, labelIds: ids }
}

export function matchesLabel(team: Labelled, filter: LabelFilter) {
  if (isAllTeams(filter)) return true
  if (filter.none && team.labelIds.length === 0) return true
  return team.labelIds.some((id) => filter.labelIds.includes(id))
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

/** Ile drużyn ma każda etykieta i ile nie ma żadnej (do liczników przy etykietach). */
export function countByFilter(teams: { labelIds: string[] }[]) {
  const byLabel = new Map<string, number>()
  let none = 0
  for (const team of teams) {
    if (team.labelIds.length === 0) none++
    for (const id of team.labelIds) byLabel.set(id, (byLabel.get(id) ?? 0) + 1)
  }
  return { all: teams.length, none, byLabel }
}

export type LabelSection<T> = { key: string; title: string; teams: T[] }

/**
 * Drużyny w sekcjach po etykietach (kolejność etykiet jak z API), na końcu „Bez etykiety”.
 * Drużyna z kilkoma etykietami jest w każdej z nich. Przy filtrze tylko wybrane sekcje; puste
 * sekcje znikają.
 */
export function groupByLabel<T extends Labelled>(
  teams: T[],
  labels: { id: string; name: string }[],
  filter: LabelFilter = ALL_TEAMS,
): LabelSection<T>[] {
  const all = isAllTeams(filter)
  const sections: LabelSection<T>[] = labels
    .filter((l) => all || filter.labelIds.includes(l.id))
    .map((l) => ({
      key: l.id,
      title: l.name,
      teams: teams.filter((t) => t.labelIds.includes(l.id)),
    }))
  if (all || filter.none) {
    sections.push({
      key: NO_LABEL,
      title: "Bez etykiety",
      teams: teams.filter((t) => t.labelIds.length === 0),
    })
  }
  return sections.filter((s) => s.teams.length > 0)
}

function normalize(text: string) {
  return text
    .toLowerCase()
    .replace(/ł/g, "l")
    .normalize("NFD")
    .replace(/\p{Diacritic}/gu, "")
    .trim()
}
