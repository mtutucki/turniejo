import type { Metadata } from "next"

import { MatchDetailView } from "@/features/public/components/match-detail-view"
import { requireSection } from "@/features/public/server"

export const metadata: Metadata = { title: "Mecz" }

export default async function PublicMatchPage({
  params,
}: PageProps<"/t/[slug]/matches/[matchId]">) {
  const { slug, matchId } = await params
  await requireSection(slug, (s) => s.showSchedule || s.showLiveMatches)
  return <MatchDetailView matchId={matchId} />
}
