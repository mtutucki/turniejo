import { MatchesOverview } from "@/features/public/components/match-sections"
import { getPublicTournament } from "@/features/public/server"

export default async function PublicTournamentPage({ params }: PageProps<"/t/[slug]">) {
  const { slug } = await params
  await getPublicTournament(slug)
  return <MatchesOverview />
}
