import { toZonedDateInput } from "@/lib/format"
import type { PublicTournamentListItem } from "@/types/public"

export type DirectorySection = "live" | "upcoming" | "finished"

export type Organizer = { id: string; name: string; count: number }

/** Bez wielkości liter i polskich znaków: "lodz" znajduje "Łódź". */
export function normalize(text: string) {
  return text
    .toLocaleLowerCase("pl-PL")
    .replace(/ł/g, "l")
    .normalize("NFD")
    .replace(/\p{Diacritic}/gu, "")
    .trim()
}

/**
 * Trwające: status „W trakcie” albo mecz na żywo. Zakończone: status „Zakończony”.
 * Reszta (zapisy, terminarz gotowy) to nadchodzące.
 */
export function sectionOf(t: PublicTournamentListItem): DirectorySection {
  if (t.status === "InProgress" || t.liveMatchCount > 0) return "live"
  if (t.status === "Finished") return "finished"
  return "upcoming"
}

export function filterTournaments(
  items: PublicTournamentListItem[],
  { query, organizerId }: { query: string; organizerId: string | null },
) {
  const q = normalize(query)
  return items.filter(
    (t) =>
      (!organizerId || t.organizer.id === organizerId) &&
      (!q || normalize(t.name).includes(q) || normalize(t.organizer.name).includes(q)),
  )
}

/**
 * Sekcje w kolejności wyświetlania. Trwające: najpierw te z meczami na żywo. Nadchodzące: od najbliższego.
 * Zakończone: od ostatnio zakończonego.
 */
export function groupTournaments(items: PublicTournamentListItem[]) {
  const by = (section: DirectorySection) => items.filter((t) => sectionOf(t) === section)
  const byStart = (a: PublicTournamentListItem, b: PublicTournamentListItem) =>
    a.startAt.localeCompare(b.startAt) || a.name.localeCompare(b.name, "pl")
  return {
    live: by("live").sort((a, b) => b.liveMatchCount - a.liveMatchCount || byStart(a, b)),
    upcoming: by("upcoming").sort(byStart),
    finished: by("finished").sort(
      (a, b) => b.endAt.localeCompare(a.endAt) || a.name.localeCompare(b.name, "pl"),
    ),
  }
}

/** Organizatorzy do filtra: od tego z największą liczbą turniejów, potem alfabetycznie. */
export function organizersOf(items: PublicTournamentListItem[]): Organizer[] {
  const map = new Map<string, Organizer>()
  for (const t of items) {
    const o = map.get(t.organizer.id) ?? { ...t.organizer, count: 0 }
    o.count++
    map.set(o.id, o)
  }
  return [...map.values()].sort((a, b) => b.count - a.count || a.name.localeCompare(b.name, "pl"))
}

const DAY_MS = 24 * 60 * 60 * 1000

/** Krótka etykieta terminu dla nadchodzącego turnieju: „Dziś”, „Jutro”, „Za 5 dni”, inaczej null. */
export function startsInLabel(startAt: string, now: Date = new Date()) {
  const days = Math.round(
    (Date.parse(toZonedDateInput(startAt)) - Date.parse(toZonedDateInput(now))) / DAY_MS,
  )
  if (days <= 0) return "Dziś"
  if (days === 1) return "Jutro"
  if (days <= 14) return `Za ${days} dni`
  return null
}
