import { toZonedDateInput } from "@/lib/format"
import type { PublicMatch, PublicTournament } from "@/types/public"

/** Filtry meczów w portalu; null = bez filtra. Dzień jako "YYYY-MM-DD" w strefie turniejów. */
export type MatchFilters = {
  teamId: string | null
  categoryId: string | null
  groupId: string | null
  pitchId: string | null
  day: string | null
}

export const NO_MATCH_FILTERS: MatchFilters = {
  teamId: null,
  categoryId: null,
  groupId: null,
  pitchId: null,
  day: null,
}

export function applyMatchFilters(matches: PublicMatch[], f: MatchFilters) {
  return matches.filter(
    (m) =>
      (!f.teamId || m.homeTournamentTeamId === f.teamId || m.awayTournamentTeamId === f.teamId) &&
      (!f.categoryId || m.categoryId === f.categoryId) &&
      (!f.groupId || m.groupId === f.groupId) &&
      (!f.pitchId || m.pitchId === f.pitchId) &&
      (!f.day || toZonedDateInput(m.scheduledAt) === f.day),
  )
}

export const hasMatchFilters = (f: MatchFilters) => Object.values(f).some((v) => v !== null)

/**
 * Co da się filtrować w tym turnieju. Select bez wyboru (jedno boisko, jedna kategoria) tylko
 * zajmowałby miejsce, więc go nie ma.
 */
export function matchFilterOptions(
  tournament: Pick<PublicTournament, "format" | "categories" | "groups" | "pitches">,
  matches: PublicMatch[],
  categoryId: string | null,
) {
  const multipleCategories = tournament.categories.length > 1
  const categoryName = (id: string) => tournament.categories.find((c) => c.id === id)?.name
  // W pucharze mecze nie należą do grup, więc grupa (np. po zmianie formatu) dawałaby pusty wynik.
  const groups =
    tournament.format === "Knockout"
      ? []
      : tournament.groups
          .filter((g) => !categoryId || g.categoryId === categoryId)
          .map((g) => ({
            id: g.id,
            // Przy kilku kategoriach i bez wybranej kategorii „Grupa A” byłaby niejednoznaczna.
            label:
              multipleCategories && !categoryId
                ? `${categoryName(g.categoryId)} · ${g.name}`
                : g.name,
          }))
  const days = [...new Set(matches.map((m) => toZonedDateInput(m.scheduledAt)))].sort()
  return {
    categories: multipleCategories ? tournament.categories : [],
    groups,
    pitches: tournament.pitches.length > 1 ? tournament.pitches : [],
    days: days.length > 1 ? days : [],
  }
}

type Params = Record<string, string | string[] | undefined>
const first = (value: string | string[] | undefined) =>
  (Array.isArray(value) ? value[0] : value) || null

/** Filtry z adresu (?team=…&cat=…&group=…&pitch=…&day=…). */
export function parseMatchFilters(params: Params): MatchFilters {
  const day = first(params.day)
  return {
    teamId: first(params.team),
    categoryId: first(params.cat),
    groupId: first(params.group),
    pitchId: first(params.pitch),
    day: day && /^\d{4}-\d{2}-\d{2}$/.test(day) ? day : null,
  }
}

export function matchFiltersToSearch(f: MatchFilters) {
  const params = new URLSearchParams()
  if (f.teamId) params.set("team", f.teamId)
  if (f.categoryId) params.set("cat", f.categoryId)
  if (f.groupId) params.set("group", f.groupId)
  if (f.pitchId) params.set("pitch", f.pitchId)
  if (f.day) params.set("day", f.day)
  return params.toString()
}
