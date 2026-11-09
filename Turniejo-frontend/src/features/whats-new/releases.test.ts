import { readFileSync } from "node:fs"

import { describe, expect, it } from "vitest"

import { CHANGELOG_PATH, parseChangelog, renderReleases, RELEASES_PATH } from "../../../scripts/sync-changelog.mjs"
import { compareVersions, MAX_ITEMS, type Release, silentlySeen, whatsNewSince } from "./releases"

const release = (version: string, sections: Record<string, string[]>): Release => ({
  version,
  date: "2026-11-09",
  intro: "",
  sections: Object.entries(sections).map(([category, items]) => ({ category, items })),
})

const RELEASES = [
  release("1.3.0", { Nowe: ["c1", "c2"], Poprawione: ["fix"] }),
  release("1.2.1", { Poprawione: ["tylko poprawka"] }),
  release("1.2.0", { Zmienione: ["b1"] }),
  release("1.1.0", { Nowe: ["a1"] }),
]

describe("okno Co nowego", () => {
  it("porównuje wersje liczbowo, nie jak tekst", () => {
    expect(compareVersions("1.10.0", "1.9.0")).toBe(1)
    expect(compareVersions("1.2.0", "1.2.0")).toBe(0)
  })

  it("kto wraca po kilku wydaniach, dostaje jedno okno ze wszystkimi zmianami, najnowsze na górze", () => {
    const result = whatsNewSince("1.1.0", RELEASES)
    expect(result).toEqual({ latest: "1.3.0", versions: ["1.3.0", "1.2.0"], items: ["c1", "c2", "b1"], hidden: 0 })
  })

  it("pokazuje tylko Nowe i Zmienione", () => {
    expect(whatsNewSince("1.2.0", RELEASES)?.items).toEqual(["c1", "c2"])
  })

  it("przycina długą listę i mówi, ile zostało", () => {
    const many = [release("2.0.0", { Nowe: Array.from({ length: MAX_ITEMS + 3 }, (_, i) => `n${i}`) })]
    const result = whatsNewSince("1.0.0", many)
    expect(result?.items).toHaveLength(MAX_ITEMS)
    expect(result?.hidden).toBe(3)
  })

  it("nowe konto nie dostaje okna, tylko zapis bieżącej wersji", () => {
    expect(whatsNewSince(null, RELEASES)).toBeNull()
    expect(silentlySeen(null, RELEASES)).toBe("1.3.0")
  })

  it("same poprawki od ostatniej wizyty: bez okna, wersja zapisana po cichu", () => {
    const fixesOnly = RELEASES.slice(1)
    expect(whatsNewSince("1.2.0", fixesOnly)).toBeNull()
    expect(silentlySeen("1.2.0", fixesOnly)).toBe("1.2.1")
  })

  it("aktualna wersja już widziana: nic do zrobienia", () => {
    expect(whatsNewSince("1.3.0", RELEASES)).toBeNull()
    expect(silentlySeen("1.3.0", RELEASES)).toBeNull()
  })
})

describe("CHANGELOG.md", () => {
  it("releases.json jest aktualny (inaczej: npm run changelog:sync)", () => {
    const current = readFileSync(RELEASES_PATH, "utf8").replace(/\r\n/g, "\n")
    expect(current).toBe(renderReleases(readFileSync(CHANGELOG_PATH, "utf8")))
  })

  it("czyta wersje, kategorie i wielolinijkowe punkty, pomija Nieopublikowane", () => {
    const parsed = parseChangelog(
      [
        "# Zmiany",
        "## [Nieopublikowane]",
        "### Nowe",
        "- jeszcze nie",
        "## [1.1.0] - 2026-12-01",
        "Opis.",
        "### Nowe",
        "- **Drabinka** w `portalu`, zobacz [pomoc](/help)",
        "  i dalszy ciąg",
        "### Poprawione",
        "",
        "## [1.0.0] - 2026-11-09",
      ].join("\n"),
    )
    expect(parsed).toEqual([
      {
        version: "1.1.0",
        date: "2026-12-01",
        intro: "Opis.",
        sections: [{ category: "Nowe", items: ["Drabinka w portalu, zobacz pomoc i dalszy ciąg"] }],
      },
      { version: "1.0.0", date: "2026-11-09", intro: "", sections: [] },
    ])
  })
})
