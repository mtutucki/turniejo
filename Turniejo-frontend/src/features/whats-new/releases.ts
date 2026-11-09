import data from "./releases.json"

// Wydane wersje z CHANGELOG.md (kopia: scripts/sync-changelog.mjs), od najnowszej.
export type ReleaseSection = { category: string; items: string[] }
export type Release = {
  version: string
  date: string | null
  intro: string
  sections: ReleaseSection[]
}

export const RELEASES: Release[] = data

/** Kategorie w oknie „Co nowego”. Poprawki i usunięcia są tylko na stronie z pełną listą zmian. */
const ANNOUNCED = ["Nowe", "Zmienione"]

/** Ile punktów mieści okno na telefonie bez przewijania. Reszta: „I jeszcze N zmian” i link. */
export const MAX_ITEMS = 5

export function compareVersions(a: string, b: string) {
  const [x, y] = [a, b].map((v) => v.split(".").map(Number))
  for (let i = 0; i < 3; i++) {
    const diff = (x![i] ?? 0) - (y![i] ?? 0)
    if (diff !== 0) return Math.sign(diff)
  }
  return 0
}

export type WhatsNew = {
  /** Najnowsza wersja, którą zapisujemy po zamknięciu okna. */
  latest: string
  /** Wersje, z których pochodzą punkty, od najnowszej. */
  versions: string[]
  items: string[]
  hidden: number
}

/**
 * Co pokazać w jednym oknie po zalogowaniu. Wszystkie wersje nowsze niż `seen` łączymy w jedną listę
 * (najnowsze punkty na górze), więc kto wraca po kilku wydaniach, dostaje jedno okno, a nie kilka.
 * `seen === null` (nowe konto) i brak zapowiadanych zmian: okna nie ma.
 */
export function whatsNewSince(seen: string | null, releases: Release[] = RELEASES): WhatsNew | null {
  const latest = releases[0]?.version
  if (!latest || seen === null) return null

  const newer = releases.filter((r) => compareVersions(r.version, seen) > 0)
  const announced = newer
    .map((r) => ({
      version: r.version,
      items: r.sections.filter((s) => ANNOUNCED.includes(s.category)).flatMap((s) => s.items),
    }))
    .filter((r) => r.items.length > 0)
  if (announced.length === 0) return null

  const all = announced.flatMap((r) => r.items)
  return {
    latest,
    versions: announced.map((r) => r.version),
    items: all.slice(0, MAX_ITEMS),
    hidden: Math.max(0, all.length - MAX_ITEMS),
  }
}

/** Którą wersję zapisać bez pokazywania okna (nowe konto albo same poprawki od ostatniej wizyty). */
export function silentlySeen(seen: string | null, releases: Release[] = RELEASES): string | null {
  const latest = releases[0]?.version
  if (!latest) return null
  if (seen !== null && compareVersions(latest, seen) <= 0) return null
  return whatsNewSince(seen, releases) ? null : latest
}
