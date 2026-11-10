import { toZonedDateInput } from "@/lib/format"
import type { PublicTournamentListItem } from "@/types/public"

export type DirectorySection = "live" | "upcoming" | "finished"

export type Organizer = { id: string; name: string; count: number }

export type DateRangePreset = "any" | "today" | "weekend" | "week" | "month" | "custom"

/** Filtry listy turniejów; daty jako "YYYY-MM-DD" w strefie turniejów. */
export type DirectoryFilters = {
  query: string
  status: DirectorySection | "all"
  when: DateRangePreset
  from: string
  to: string
  organizerId: string | null
  category: string | null
}

export const EMPTY_FILTERS: DirectoryFilters = {
  query: "",
  status: "all",
  when: "any",
  from: "",
  to: "",
  organizerId: null,
  category: null,
}

export const STATUS_LABELS: Record<DirectorySection, string> = {
  live: "Trwają teraz",
  upcoming: "Nadchodzące",
  finished: "Zakończone",
}

export const WHEN_LABELS: Record<Exclude<DateRangePreset, "any">, string> = {
  today: "Dziś",
  weekend: "Ten weekend",
  week: "Najbliższe 7 dni",
  month: "Ten miesiąc",
  custom: "Wybrane daty",
}

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

const DAY_MS = 24 * 60 * 60 * 1000

function addDays(date: string, days: number) {
  return new Date(Date.parse(date) + days * DAY_MS).toISOString().slice(0, 10)
}

/** Zakres dat dla wybranego terminu (włącznie) albo null, gdy termin dowolny lub niepełny. */
export function dateRange(
  { when, from, to }: Pick<DirectoryFilters, "when" | "from" | "to">,
  now: Date = new Date(),
): { from: string; to: string } | null {
  const today = toZonedDateInput(now)
  // Dzień tygodnia liczony z daty w strefie turniejów, nie z zegara przeglądarki.
  const weekday = new Date(today).getUTCDay() // 0 = niedziela
  switch (when) {
    case "today":
      return { from: today, to: today }
    case "weekend": {
      // W sobotę i niedzielę „ten weekend” to trwający; w tygodniu najbliższy.
      if (weekday === 0) return { from: today, to: today }
      const saturday = addDays(today, 6 - weekday)
      return { from: saturday, to: addDays(saturday, 1) }
    }
    case "week":
      return { from: today, to: addDays(today, 6) }
    case "month": {
      const first = `${today.slice(0, 7)}-01`
      const next = new Date(first)
      next.setUTCMonth(next.getUTCMonth() + 1)
      return { from: first, to: addDays(next.toISOString().slice(0, 10), -1) }
    }
    case "custom":
      if (!from && !to) return null
      return { from: from || "0000-01-01", to: to || "9999-12-31" }
    default:
      return null
  }
}

export function filterTournaments(
  items: PublicTournamentListItem[],
  filters: Partial<DirectoryFilters>,
  now: Date = new Date(),
) {
  const f = { ...EMPTY_FILTERS, ...filters }
  const q = normalize(f.query)
  const range = dateRange(f, now)
  const category = f.category && normalize(f.category)
  return items.filter((t) => {
    if (f.organizerId && t.organizer.id !== f.organizerId) return false
    if (f.status !== "all" && sectionOf(t) !== f.status) return false
    if (q && !normalize(t.name).includes(q) && !normalize(t.organizer.name).includes(q))
      return false
    if (category && !t.categories.some((c) => normalize(c) === category)) return false
    if (range) {
      // Turniej pasuje, gdy choć jeden jego dzień wypada w zakresie.
      const start = toZonedDateInput(t.startAt)
      const end = toZonedDateInput(t.endAt)
      if (start > range.to || end < range.from) return false
    }
    return true
  })
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

/** Organizatorzy do filtra: alfabetycznie, z liczbą turniejów. */
export function organizersOf(items: PublicTournamentListItem[]): Organizer[] {
  const map = new Map<string, Organizer>()
  for (const t of items) {
    const o = map.get(t.organizer.id) ?? { ...t.organizer, count: 0 }
    o.count++
    map.set(o.id, o)
  }
  return [...map.values()].sort((a, b) => a.name.localeCompare(b.name, "pl"))
}

/** Kategorie wszystkich turniejów bez powtórzeń, w kolejności „U8, U10, U12”. */
export function categoriesOf(items: PublicTournamentListItem[]) {
  const map = new Map<string, string>()
  for (const name of items.flatMap((t) => t.categories)) {
    const key = normalize(name)
    if (!map.has(key)) map.set(key, name)
  }
  return [...map.values()].sort((a, b) => a.localeCompare(b, "pl", { numeric: true }))
}

type Params = Record<string, string | string[] | undefined>
const first = (value: string | string[] | undefined) => (Array.isArray(value) ? value[0] : value)
const isDate = (value: string | undefined): value is string =>
  !!value && /^\d{4}-\d{2}-\d{2}$/.test(value)

/** Filtry z adresu (?q=…&status=…&when=…&from=…&to=…&org=…&cat=…); nieznane wartości pomija. */
export function parseFilters(params: Params): DirectoryFilters {
  const status = first(params.status)
  const when = first(params.when)
  const from = first(params.from)
  const to = first(params.to)
  return {
    query: first(params.q) ?? "",
    status: status === "live" || status === "upcoming" || status === "finished" ? status : "all",
    when: when && when in WHEN_LABELS ? (when as DateRangePreset) : "any",
    from: isDate(from) ? from : "",
    to: isDate(to) ? to : "",
    organizerId: first(params.org) || null,
    category: first(params.cat) || null,
  }
}

export function filtersToSearch(f: DirectoryFilters) {
  const params = new URLSearchParams()
  if (f.query) params.set("q", f.query)
  if (f.status !== "all") params.set("status", f.status)
  if (f.when !== "any") params.set("when", f.when)
  if (f.when === "custom" && f.from) params.set("from", f.from)
  if (f.when === "custom" && f.to) params.set("to", f.to)
  if (f.organizerId) params.set("org", f.organizerId)
  if (f.category) params.set("cat", f.category)
  return params.toString()
}

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
