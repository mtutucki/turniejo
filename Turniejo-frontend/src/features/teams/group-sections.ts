import type { TournamentGroup } from "@/types/tournaments"

export type GroupSection<T> = { group: TournamentGroup | null; teams: T[] }

/**
 * Drużyny kategorii podzielone na grupy (w kolejności grup), żeby na liście było widać skład każdej.
 * Drużyny bez grupy (albo z grupą, której już nie ma) idą do sekcji z `group: null` na początku,
 * bo bez grupy nie zagrają i trzeba je zauważyć. Zwraca null, gdy podział nic nie daje:
 * brak grup albo jedna grupa, w której są wszystkie drużyny.
 */
export function groupSections<T extends { groupId: string | null }>(
  teams: T[],
  groups: TournamentGroup[],
): GroupSection<T>[] | null {
  const sorted = [...groups].sort((a, b) => a.order - b.order)
  const known = new Set(sorted.map((g) => g.id))
  const unassigned = teams.filter((t) => !t.groupId || !known.has(t.groupId))
  if (sorted.length === 0 || (sorted.length === 1 && unassigned.length === 0)) return null

  const sections: GroupSection<T>[] = sorted.map((group) => ({
    group,
    teams: teams.filter((t) => t.groupId === group.id),
  }))
  return unassigned.length > 0 ? [{ group: null, teams: unassigned }, ...sections] : sections
}
