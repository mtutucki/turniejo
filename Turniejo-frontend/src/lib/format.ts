/**
 * Strefa czasowa turniejów. Backend trzyma momenty w UTC, a turniej nie ma własnej strefy — bez stałej
 * strefy organizator, sędzia i kibic z przeglądarkami w różnych strefach widzieliby różne godziny meczu.
 */
export const APP_TIME_ZONE = "Europe/Warsaw"

const dateFormatter = new Intl.DateTimeFormat("pl-PL", {
  day: "numeric",
  month: "short",
  year: "numeric",
  timeZone: APP_TIME_ZONE,
})
const dateTimeFormatter = new Intl.DateTimeFormat("pl-PL", {
  day: "numeric",
  month: "short",
  hour: "2-digit",
  minute: "2-digit",
  timeZone: APP_TIME_ZONE,
})
const timeFormatter = new Intl.DateTimeFormat("pl-PL", {
  hour: "2-digit",
  minute: "2-digit",
  timeZone: APP_TIME_ZONE,
})
const dayFormatter = new Intl.DateTimeFormat("pl-PL", {
  weekday: "long",
  day: "numeric",
  month: "long",
  timeZone: APP_TIME_ZONE,
})

// Daty urodzenia to północ UTC — formatujemy w UTC, inaczej strefa z ujemnym offsetem
// pokazałaby dzień wcześniej.
const birthDateFormatter = new Intl.DateTimeFormat("pl-PL", {
  dateStyle: "medium",
  timeZone: "UTC",
})

export function formatBirthDate(value: string) {
  return birthDateFormatter.format(new Date(value))
}

export function formatDate(value: string | Date) {
  return dateFormatter.format(new Date(value))
}

export function formatDateTime(value: string | Date) {
  return dateTimeFormatter.format(new Date(value))
}

/** "09:35" */
export function formatTime(value: string | Date) {
  return timeFormatter.format(new Date(value))
}

/** "sobota, 12 grudnia" */
export function formatDayLong(value: string | Date) {
  return dayFormatter.format(new Date(value))
}

/** "12 lip 2026 – 14 lip 2026", albo jedna data dla turnieju jednodniowego. */
export function formatDateRange(start: string, end: string) {
  const from = formatDate(start)
  const to = formatDate(end)
  return from === to ? from : `${from} – ${to}`
}

const pluralRules = new Intl.PluralRules("pl-PL")

/** Polska odmiana: plural(2, ["drużyna", "drużyny", "drużyn"]) → "2 drużyny". */
export function plural(count: number, [one, few, many]: [string, string, string]) {
  const form = pluralRules.select(count)
  return `${count} ${form === "one" ? one : form === "few" ? few : many}`
}

// --- Czas lokalny turnieju <-> UTC -----------------------------------------------------------

const partsFormatter = new Intl.DateTimeFormat("en-US", {
  timeZone: APP_TIME_ZONE,
  hourCycle: "h23",
  year: "numeric",
  month: "2-digit",
  day: "2-digit",
  hour: "2-digit",
  minute: "2-digit",
  second: "2-digit",
})

function zonedParts(date: Date) {
  const parts = Object.fromEntries(
    partsFormatter.formatToParts(date).map((p) => [p.type, p.value]),
  ) as Record<string, string>
  return {
    year: parts.year!,
    month: parts.month!,
    day: parts.day!,
    hour: parts.hour!,
    minute: parts.minute!,
    second: parts.second!,
  }
}

/** Offset strefy turnieju względem UTC w danym momencie (ms) — uwzględnia czas letni. */
function zoneOffsetMs(date: Date) {
  const p = zonedParts(date)
  const asUtc = Date.UTC(+p.year, +p.month - 1, +p.day, +p.hour, +p.minute, +p.second)
  return asUtc - Math.floor(date.getTime() / 1000) * 1000
}

/** "2026-12-12" (dzień w strefie turnieju) — klucz do grupowania i wartość <input type="date">. */
export function toZonedDateInput(value: string | Date) {
  const p = zonedParts(new Date(value))
  return `${p.year}-${p.month}-${p.day}`
}

/** ISO (z backendu) → wartość <input type="datetime-local"> w strefie turnieju. */
export function toDateTimeLocal(iso: string) {
  const p = zonedParts(new Date(iso))
  return `${p.year}-${p.month}-${p.day}T${p.hour}:${p.minute}`
}

/** Wartość <input type="datetime-local"> (czas w strefie turnieju) → ISO w UTC. */
export function fromDateTimeLocal(value: string) {
  const [date, time] = value.split("T") as [string, string]
  const [y, mo, d] = date.split("-").map(Number) as [number, number, number]
  const [h, mi] = time.split(":").map(Number) as [number, number]
  const wallClockAsUtc = Date.UTC(y, mo - 1, d, h, mi)
  // Dwa przebiegi: offset liczony dla przybliżonego momentu może się różnić na granicy czasu letniego.
  let utc = wallClockAsUtc - zoneOffsetMs(new Date(wallClockAsUtc))
  utc = wallClockAsUtc - zoneOffsetMs(new Date(utc))
  return new Date(utc).toISOString()
}

/** Dzień (<input type="date">) + godzina (<input type="time">) w strefie turnieju → ISO w UTC. */
export function zonedToIso(date: string, time: string) {
  return fromDateTimeLocal(`${date}T${time}`)
}

// --- Daty bez godziny (daty urodzenia) -------------------------------------------------------

/** DateTime z backendu ("2015-01-01T00:00:00") → wartość <input type="date">. */
export function toDateInput(value: string | null) {
  return value ? value.slice(0, 10) : ""
}

/** Wartość <input type="date"> → północ UTC (kolumny dat urodzenia to timestamptz). */
export function fromDateInput(value: string) {
  return value ? `${value}T00:00:00Z` : null
}
