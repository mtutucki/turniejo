import { APP_TIME_ZONE, formatDateTime } from "@/lib/format"
import { absoluteUrl, SITE_NAME } from "@/lib/site"
import type { MatchLivePanel } from "@/types/matches"
import type { PublicTournament } from "@/types/public"

// Teksty i dane strukturalne (schema.org) dla wyszukiwarek i podglądów linków. Czyste funkcje,
// żeby dało się je sprawdzić testem bez renderowania strony.

type TournamentInfo = Pick<
  PublicTournament,
  "name" | "slug" | "status" | "startAt" | "endAt" | "description" | "categories"
>
type MatchInfo = Pick<
  MatchLivePanel,
  | "matchId"
  | "status"
  | "scheduledAt"
  | "homeScore"
  | "awayScore"
  | "homePenaltyScore"
  | "awayPenaltyScore"
  | "homeTeam"
  | "awayTeam"
>

const longDate = new Intl.DateTimeFormat("pl-PL", {
  day: "numeric",
  month: "long",
  year: "numeric",
  timeZone: APP_TIME_ZONE,
})

/** „9–10 maja 2026”, „30 maja – 1 czerwca 2026”, „9 maja 2026”. */
export function tournamentDates(startAt: string, endAt: string) {
  // Intl daje wąskie spacje (U+2009) wokół półpauzy, w tytułach i opisach wolimy zwykłe.
  return longDate.formatRange(new Date(startAt), new Date(endAt)).replace(/\s/g, " ")
}

/** Opis do Google i podglądu linku: data, kategorie i co jest w portalu. Opis organizatora ma pierwszeństwo. */
export function tournamentDescription(t: TournamentInfo) {
  const own = t.description?.trim()
  if (own) return truncate(own, 160)
  const categories = t.categories.length
    ? ` Kategorie: ${[...t.categories]
        .sort((a, b) => a.order - b.order)
        .map((c) => c.name)
        .join(", ")}.`
    : ""
  return `${t.name}, ${tournamentDates(t.startAt, t.endAt)}. Wyniki na żywo, terminarz i tabele turnieju piłkarskiego.${categories}`
}

const hasScore = (m: MatchInfo) =>
  m.status === "Live" || m.status === "Paused" || m.status === "Finished"

/** „Orły – Lwy 2:1”, „Orły – Lwy 1:1 (karne 4:3)”, przed meczem „Orły – Lwy”. */
export function matchTitle(m: MatchInfo) {
  const teams = `${m.homeTeam.name} – ${m.awayTeam.name}`
  if (!hasScore(m)) return teams
  const penalties =
    m.homePenaltyScore != null && m.awayPenaltyScore != null
      ? ` (karne ${m.homePenaltyScore}:${m.awayPenaltyScore})`
      : ""
  return `${teams} ${m.homeScore}:${m.awayScore}${penalties}`
}

export function matchDescription(m: MatchInfo, tournamentName: string) {
  const when = formatDateTime(m.scheduledAt)
  if (m.status === "Live" || m.status === "Paused")
    return `Na żywo: ${matchTitle(m)}. ${tournamentName}, wynik i przebieg meczu.`
  if (m.status === "Finished")
    return `Wynik meczu ${matchTitle(m)}, ${when}. ${tournamentName}: przebieg meczu, gole i kartki.`
  if (m.status === "Cancelled") return `Mecz ${matchTitle(m)} odwołany. ${tournamentName}.`
  return `Mecz ${matchTitle(m)}, ${when}. ${tournamentName}: wynik na żywo w trakcie meczu.`
}

const EVENT_STATUS: Partial<Record<TournamentInfo["status"] | MatchInfo["status"], string>> = {
  Cancelled: "https://schema.org/EventCancelled",
  Postponed: "https://schema.org/EventPostponed",
}

function eventStatus(status: TournamentInfo["status"] | MatchInfo["status"]) {
  return EVENT_STATUS[status] ?? "https://schema.org/EventScheduled"
}

/** schema.org SportsEvent turnieju. Bez zawodników: portal i tak skraca nazwiska (RODO). */
export function tournamentJsonLd(t: TournamentInfo) {
  const url = absoluteUrl(`/t/${t.slug}`)
  return {
    "@context": "https://schema.org",
    "@type": "SportsEvent",
    name: t.name,
    description: tournamentDescription(t),
    sport: "Soccer",
    startDate: t.startAt,
    endDate: t.endAt,
    eventStatus: eventStatus(t.status),
    eventAttendanceMode: "https://schema.org/OfflineEventAttendanceMode",
    url,
    image: absoluteUrl(`/t/${t.slug}/opengraph-image`),
  }
}

export function matchJsonLd(m: MatchInfo, t: Pick<TournamentInfo, "name" | "slug">) {
  return {
    "@context": "https://schema.org",
    "@type": "SportsEvent",
    name: `${m.homeTeam.name} – ${m.awayTeam.name}`,
    description: matchDescription(m, t.name),
    sport: "Soccer",
    startDate: m.scheduledAt,
    eventStatus: eventStatus(m.status),
    eventAttendanceMode: "https://schema.org/OfflineEventAttendanceMode",
    url: absoluteUrl(`/t/${t.slug}/matches/${m.matchId}`),
    homeTeam: { "@type": "SportsTeam", name: m.homeTeam.name },
    awayTeam: { "@type": "SportsTeam", name: m.awayTeam.name },
    superEvent: { "@type": "SportsEvent", name: t.name, url: absoluteUrl(`/t/${t.slug}`) },
  }
}

export function websiteJsonLd() {
  return {
    "@context": "https://schema.org",
    "@type": "WebSite",
    name: SITE_NAME,
    url: absoluteUrl("/"),
    inLanguage: "pl-PL",
    description: "Wyniki, terminarze i tabele turniejów piłkarskich na żywo.",
  }
}

/** JSON w <script type="application/ld+json">: „<” zamienione, żeby nazwa drużyny nie zamknęła skryptu. */
export function jsonLdScript(data: unknown) {
  return JSON.stringify(data).replace(/</g, "\\u003c")
}

function truncate(text: string, max: number) {
  const flat = text.replace(/\s+/g, " ")
  if (flat.length <= max) return flat
  const cut = flat.slice(0, max - 1)
  return `${cut.slice(0, cut.lastIndexOf(" ") > max / 2 ? cut.lastIndexOf(" ") : cut.length)}…`
}
