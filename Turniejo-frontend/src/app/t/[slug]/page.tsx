import { MatchesOverview } from "@/features/public/components/match-sections"
import { getPublicTournament } from "@/features/public/server"

export default async function PublicTournamentPage({
  params,
  searchParams,
}: PageProps<"/t/[slug]">) {
  const { slug } = await params
  const { team } = await searchParams
  await getPublicTournament(slug)
  return <MatchesOverview initialTeamId={typeof team === "string" ? team : undefined} />
}
