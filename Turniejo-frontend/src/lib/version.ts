/** `label` do stopki (sam numer wydania), `details` do podpowiedzi (z liczbą zmian po wydaniu i commitem). */
export type AppVersion = { label: string; details: string }

/**
 * Wersja aplikacji do pokazania ludziom. Źródło: `git describe --tags --always` z serwera przy budowie
 * (NEXT_PUBLIC_APP_VERSION), np. "v1.2.0" (dokładnie wydanie) albo "v1.2.0-3-gabc1234" (3 commity po
 * wydaniu 1.2.0). Użytkownik widzi tylko "1.2.0", pełny zapis "1.2.0+3 (abc1234)" jest w podpowiedzi
 * dla zgłoszeń błędów. Lokalnie (bez zmiennej) nic nie pokazujemy. Zasady: docs/wersjonowanie.md.
 */
export function formatVersion(
  raw: string | undefined = process.env.NEXT_PUBLIC_APP_VERSION,
): AppVersion | null {
  const value = raw?.trim()
  if (!value || value === "dev") return null

  const afterRelease = value.match(/^v?(\d+\.\d+\.\d+)-(\d+)-g([0-9a-f]+)$/)
  if (afterRelease) {
    const [, version, commits, sha] = afterRelease
    return { label: version!, details: `${version}+${commits} (${sha!.slice(0, 7)})` }
  }

  const release = value.match(/^v?(\d+\.\d+\.\d+)$/)
  if (release) return { label: release[1]!, details: release[1]! }

  // Repozytorium bez żadnego tagu: describe zwraca sam skrót commita.
  if (!/^[0-9a-f]{7,40}$/.test(value)) return null
  const sha = value.slice(0, 7)
  return { label: sha, details: sha }
}
