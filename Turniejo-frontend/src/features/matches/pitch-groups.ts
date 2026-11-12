import type { Pitch } from "@/types/matches"
import type { TournamentCategory, TournamentGroup } from "@/types/tournaments"

/** Nazwa grupy dla boiska: przy kilku kategoriach z kategorią z przodu, grupa fazy 2 z dopiskiem. */
export function pitchGroupName(
  group: TournamentGroup,
  categories: Pick<TournamentCategory, "id" | "name">[],
): string {
  const name = group.stage === 2 ? `${group.name} (faza 2)` : group.name
  const category = categories.length > 1 && categories.find((c) => c.id === group.categoryId)
  return category ? `${category.name} · ${name}` : name
}

/** Grupy do wyboru przy boisku: po kategoriach (w ich kolejności), najpierw faza 1, potem faza 2. */
export function pitchGroupSections(
  categories: Pick<TournamentCategory, "id" | "name" | "order">[],
  groups: TournamentGroup[],
) {
  return [...categories]
    .sort((a, b) => a.order - b.order)
    .map((category) => ({
      category,
      groups: groups
        .filter((g) => g.categoryId === category.id)
        .sort((a, b) => (a.stage ?? 1) - (b.stage ?? 1) || a.order - b.order),
    }))
    .filter((section) => section.groups.length > 0)
}

/** Grupy przypisane do boiska, w kolejności sekcji; puste = boisko dla wszystkich grup. */
export function pitchGroupsLabel(
  pitch: Pick<Pitch, "groupIds">,
  categories: Pick<TournamentCategory, "id" | "name" | "order">[],
  groups: TournamentGroup[],
): string | null {
  const assigned = new Set(pitch.groupIds)
  const names = pitchGroupSections(categories, groups)
    .flatMap((section) => section.groups)
    .filter((g) => assigned.has(g.id))
    .map((g) => pitchGroupName(g, categories))
  return names.length ? names.join(", ") : null
}

/**
 * Grupy, które nie mają gdzie grać: żadne z wybranych boisk nie przyjmuje ich meczów. Boisko bez
 * przypisanych grup przyjmuje wszystkie (tak samo liczy generator w backendzie).
 */
export function groupsWithoutPitch(
  groups: TournamentGroup[],
  pitches: Pick<Pitch, "id" | "groupIds">[],
  selectedPitchIds: string[],
): TournamentGroup[] {
  const selected = pitches.filter((p) => selectedPitchIds.includes(p.id))
  if (selected.length === 0) return []
  return groups.filter(
    (g) => !selected.some((p) => p.groupIds.length === 0 || p.groupIds.includes(g.id)),
  )
}
