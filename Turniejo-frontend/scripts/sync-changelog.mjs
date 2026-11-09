// Wydane wersje z ../CHANGELOG.md jako JSON dla okna "Co nowego" i strony /changelog. Frontend buduje
// się z samego katalogu Turniejo-frontend (Docker), więc nie widzi CHANGELOG.md - stąd kopia w repo.
//
//   node scripts/sync-changelog.mjs           zapisuje src/features/whats-new/releases.json
//   node scripts/sync-changelog.mjs --check   kod 1, jeśli plik nie zgadza się z CHANGELOG.md
//
// release.sh prepare uruchamia zapis sam. Zgodność pilnuje test (releases.test.ts) w check.sh i CI.
import { readFileSync, writeFileSync } from "node:fs"
import { dirname, join } from "node:path"
import { fileURLToPath } from "node:url"

const FRONTEND = join(dirname(fileURLToPath(import.meta.url)), "..")
export const CHANGELOG_PATH = join(FRONTEND, "..", "CHANGELOG.md")
export const RELEASES_PATH = join(FRONTEND, "src", "features", "whats-new", "releases.json")

// Markdown w linijce do zwykłego tekstu: **pogrubienie**, `kod`, [tekst](link).
function plain(text) {
  return text
    .replace(/\[([^\]]+)\]\([^)]*\)/g, "$1")
    .replace(/\*\*([^*]+)\*\*/g, "$1")
    .replace(/`([^`]+)`/g, "$1")
    .trim()
}

/** Sekcje `## [X.Y.Z] - data` od najnowszej. „Nieopublikowane” pomijamy: okno pokazuje tylko wydania. */
export function parseChangelog(markdown) {
  const releases = []
  let release = null
  let section = null
  let item = null

  for (const raw of markdown.split(/\r?\n/)) {
    const line = raw.trimEnd()
    const heading = line.match(/^## \[(.+?)\](?:\s*-\s*(\d{4}-\d{2}-\d{2}))?/)
    if (heading) {
      const [, version, date] = heading
      release = /^\d+\.\d+\.\d+$/.test(version) ? { version, date: date ?? null, intro: "", sections: [] } : null
      if (release) releases.push(release)
      section = item = null
      continue
    }
    if (!release) continue

    const category = line.match(/^### (.+)$/)
    if (category) {
      section = { category: category[1].trim(), items: [] }
      release.sections.push(section)
      item = null
      continue
    }

    const bullet = line.match(/^[-*] (.+)$/)
    if (bullet && section) {
      item = plain(bullet[1])
      section.items.push(item)
      continue
    }

    if (!line.trim()) {
      item = null
      continue
    }

    // Dalszy ciąg punktu (wcięty) albo opis wersji przed pierwszą kategorią.
    if (item !== null && section && /^\s/.test(raw)) {
      section.items[section.items.length - 1] = item = `${item} ${plain(line)}`
    } else if (!section) {
      release.intro = release.intro ? `${release.intro} ${plain(line)}` : plain(line)
    }
  }

  return releases.map((r) => ({ ...r, sections: r.sections.filter((s) => s.items.length > 0) }))
}

export function renderReleases(markdown) {
  return `${JSON.stringify(parseChangelog(markdown), null, 2)}\n`
}

if (process.argv[1] && fileURLToPath(import.meta.url) === process.argv[1]) {
  const expected = renderReleases(readFileSync(CHANGELOG_PATH, "utf8"))
  if (process.argv.includes("--check")) {
    let current = ""
    try {
      current = readFileSync(RELEASES_PATH, "utf8")
    } catch {}
    if (current.replace(/\r\n/g, "\n") !== expected) {
      console.error("releases.json nie zgadza się z CHANGELOG.md - uruchom: npm run changelog:sync")
      process.exit(1)
    }
  } else {
    writeFileSync(RELEASES_PATH, expected)
    console.log(`Zapisano ${RELEASES_PATH}`)
  }
}
