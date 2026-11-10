import type { Metadata } from "next"

import { MatchDetailView } from "@/features/public/components/match-detail-view"
import { jsonLdScript, matchDescription, matchJsonLd, matchTitle } from "@/features/public/seo"
import { getPublicMatch, getPublicTournament, requireSection } from "@/features/public/server"
import { OPEN_GRAPH_DEFAULTS } from "@/lib/site"

const visible = (s: { showSchedule: boolean; showLiveMatches: boolean }) =>
  s.showSchedule || s.showLiveMatches

// „Orły – Lwy 2:1 · Puchar Wiosny” zamiast samego „Mecz”: tak wygląda wynik w Google i w podglądzie linku.
export async function generateMetadata({
  params,
}: PageProps<"/t/[slug]/matches/[matchId]">): Promise<Metadata> {
  const { slug, matchId } = await params
  const tournament = await getPublicTournament(slug)
  const match = visible(tournament.settings)
    ? await getPublicMatch(tournament.id, matchId)
    : undefined
  if (!match) return { title: "Mecz" }
  const title = matchTitle(match)
  const description = matchDescription(match, tournament.name)
  const path = `/t/${tournament.slug}/matches/${match.matchId}`
  return {
    title,
    description,
    alternates: { canonical: path },
    openGraph: {
      ...OPEN_GRAPH_DEFAULTS,
      type: "article",
      title: `${title} · ${tournament.name}`,
      description,
      url: path,
    },
  }
}

export default async function PublicMatchPage({
  params,
}: PageProps<"/t/[slug]/matches/[matchId]">) {
  const { slug, matchId } = await params
  const tournament = await requireSection(slug, visible)
  const match = await getPublicMatch(tournament.id, matchId)
  return (
    <>
      {match && (
        <script
          type="application/ld+json"
          dangerouslySetInnerHTML={{ __html: jsonLdScript(matchJsonLd(match, tournament)) }}
        />
      )}
      <MatchDetailView matchId={matchId} />
    </>
  )
}
