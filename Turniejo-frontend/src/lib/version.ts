/**
 * Wersja aplikacji do pokazania ludziom. Źródło: `git describe --tags --always` z serwera przy budowie
 * (NEXT_PUBLIC_APP_VERSION), np. "v1.2.0" (dokładnie wydanie) albo "v1.2.0-3-gabc1234" (3 commity po
 * wydaniu 1.2.0). Lokalnie (bez zmiennej) nic nie pokazujemy. Zasady wersjonowania: docs/wersjonowanie.md.
 */
export function formatVersion(
  raw: string | undefined = process.env.NEXT_PUBLIC_APP_VERSION,
): string | null {
  const value = raw?.trim()
  if (!value || value === "dev") return null

  const afterRelease = value.match(/^v?(\d+\.\d+\.\d+)-(\d+)-g([0-9a-f]+)$/)
  if (afterRelease) {
    const [, version, commits, sha] = afterRelease
    return `${version}+${commits} (${sha!.slice(0, 7)})`
  }

  const release = value.match(/^v?(\d+\.\d+\.\d+)$/)
  if (release) return release[1]!

  // Repozytorium bez żadnego tagu: describe zwraca sam skrót commita.
  return /^[0-9a-f]{7,40}$/.test(value) ? value.slice(0, 7) : null
}
