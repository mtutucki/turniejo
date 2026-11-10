import type { Metadata } from "next"

import { TeamView } from "@/features/public/components/team-view"
import { teamDescription } from "@/features/public/seo"
import { getPublicTeam, getPublicTournament, requireSection } from "@/features/public/server"
import { OPEN_GRAPH_DEFAULTS } from "@/lib/site"

export async function generateMetadata({
  params,
}: PageProps<"/t/[slug]/teams/[teamId]">): Promise<Metadata> {
  const { slug, teamId } = await params
  const tournament = await getPublicTournament(slug)
  if (!tournament.settings.showTeams) return { title: "Drużyna" }
  const team = await getPublicTeam(slug, teamId)
  const description = teamDescription(team.name, tournament)
  const path = `/t/${tournament.slug}/teams/${team.tournamentTeamId}`
  return {
    title: team.name,
    description,
    alternates: { canonical: path },
    openGraph: {
      ...OPEN_GRAPH_DEFAULTS,
      images: [{ url: `/t/${tournament.slug}/opengraph-image`, width: 1200, height: 630 }],
      title: `${team.name} · ${tournament.name}`,
      description,
      url: path,
    },
  }
}

// Strona drużyny: mecze z wynikami, bilans, puchar i skład. Bez „Pokazuj drużyny” portal nie zna
// nazw drużyn, więc strony też nie ma (jak listy drużyn).
export default async function PublicTeamPage({ params }: PageProps<"/t/[slug]/teams/[teamId]">) {
  const { slug, teamId } = await params
  await requireSection(slug, (s) => s.showTeams)
  await getPublicTeam(slug, teamId)
  return <TeamView teamId={teamId} />
}
