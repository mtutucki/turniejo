import { describe, expect, it } from "vitest"

import {
  jsonLdScript,
  matchDescription,
  matchJsonLd,
  matchTitle,
  tournamentDates,
  tournamentDescription,
  tournamentJsonLd,
} from "./seo"

const tournament = {
  name: "Puchar Wiosny",
  slug: "puchar-wiosny",
  status: "InProgress" as const,
  startAt: "2026-05-09T07:00:00Z",
  endAt: "2026-05-10T15:00:00Z",
  description: null,
  categories: [
    { id: "2", name: "U12", order: 2 },
    { id: "1", name: "U10", order: 1 },
  ],
}

const team = (name: string) => ({
  tournamentTeamId: name,
  teamId: name,
  name,
  shortName: null,
  players: [],
})
const match = {
  matchId: "m1",
  status: "Finished" as const,
  scheduledAt: "2026-05-09T08:00:00Z",
  homeScore: 2,
  awayScore: 1,
  homePenaltyScore: null,
  awayPenaltyScore: null,
  homeTeam: team("Orły"),
  awayTeam: team("Lwy"),
}

describe("tournamentDescription", () => {
  it("bez opisu organizatora: daty i kategorie w kolejności", () => {
    expect(tournamentDescription(tournament)).toBe(
      "Puchar Wiosny, 9–10 maja 2026. Wyniki na żywo, terminarz i tabele turnieju piłkarskiego. Kategorie: U10, U12.",
    )
  })

  it("opis organizatora ma pierwszeństwo i jest skracany do 160 znaków", () => {
    const long = "Słowo ".repeat(60)
    const description = tournamentDescription({ ...tournament, description: long })
    expect(description.length).toBeLessThanOrEqual(160)
    expect(description.endsWith("…")).toBe(true)
    expect(tournamentDescription({ ...tournament, description: "  Turniej  dla\nklubów " })).toBe(
      "Turniej dla klubów",
    )
  })
})

describe("tournamentDates", () => {
  it("jeden dzień, kilka dni i przełom miesięcy", () => {
    expect(tournamentDates("2026-05-09T07:00:00Z", "2026-05-09T15:00:00Z")).toBe("9 maja 2026")
    expect(tournamentDates("2026-05-30T07:00:00Z", "2026-06-01T15:00:00Z")).toBe(
      "30 maja – 1 czerwca 2026",
    )
  })
})

describe("matchTitle", () => {
  it("z wynikiem po rozpoczęciu, bez wyniku przed", () => {
    expect(matchTitle(match)).toBe("Orły – Lwy 2:1")
    expect(matchTitle({ ...match, status: "Scheduled" })).toBe("Orły – Lwy")
    expect(matchTitle({ ...match, homeScore: 1, homePenaltyScore: 4, awayPenaltyScore: 3 })).toBe(
      "Orły – Lwy 1:1 (karne 4:3)",
    )
  })

  it("opis mówi, czy mecz trwa, czy się skończył", () => {
    expect(matchDescription({ ...match, status: "Live" }, "Puchar")).toMatch(
      /^Na żywo: Orły – Lwy 2:1/,
    )
    expect(matchDescription(match, "Puchar")).toMatch(/^Wynik meczu Orły – Lwy 2:1/)
  })
})

describe("dane strukturalne", () => {
  it("turniej i mecz jako SportsEvent z adresami bezwzględnymi", () => {
    const t = tournamentJsonLd(tournament)
    expect(t["@type"]).toBe("SportsEvent")
    expect(t.url).toMatch(/^https?:\/\/.+\/t\/puchar-wiosny$/)
    const m = matchJsonLd(match, tournament)
    expect(m.homeTeam).toEqual({ "@type": "SportsTeam", name: "Orły" })
    expect(m.superEvent.url).toBe(t.url)
    expect(JSON.stringify(m)).not.toContain("players")
  })

  it("odwołany turniej ma status EventCancelled", () => {
    expect(tournamentJsonLd({ ...tournament, status: "Cancelled" }).eventStatus).toBe(
      "https://schema.org/EventCancelled",
    )
  })

  it("nazwa drużyny nie zamknie skryptu", () => {
    expect(jsonLdScript({ name: "</script><b>" })).not.toContain("</script>")
  })
})
