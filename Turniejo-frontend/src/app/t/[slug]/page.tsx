import { MatchesOverview } from "@/features/public/components/match-sections"
import { parseMatchFilters } from "@/features/public/match-filters"
import { getPublicTournament } from "@/features/public/server"

export default async function PublicTournamentPage({
  params,
  searchParams,
}: PageProps<"/t/[slug]">) {
  const { slug } = await params
  await getPublicTournament(slug)
  return <MatchesOverview initialFilters={parseMatchFilters(await searchParams)} />
}
