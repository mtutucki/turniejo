import { MatchesOverview } from "@/features/public/components/match-sections"
import { parseMatchFilters } from "@/features/public/match-filters"
import type { Metadata } from "next"

import { getPublicTournament } from "@/features/public/server"

// Filtry (?team=…&day=…) to ta sama strona dla Google: kanoniczny adres bez nich.
export async function generateMetadata({ params }: PageProps<"/t/[slug]">): Promise<Metadata> {
  const tournament = await getPublicTournament((await params).slug)
  return { alternates: { canonical: `/t/${tournament.slug}` } }
}

export default async function PublicTournamentPage({
  params,
  searchParams,
}: PageProps<"/t/[slug]">) {
  const { slug } = await params
  await getPublicTournament(slug)
  return <MatchesOverview initialFilters={parseMatchFilters(await searchParams)} />
}
