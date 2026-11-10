import type { Metadata } from "next"

import { TeamsView } from "@/features/public/components/teams-view"
import { portalPageMetadata, requireSection } from "@/features/public/server"

export async function generateMetadata({
  params,
}: PageProps<"/t/[slug]/teams">): Promise<Metadata> {
  return portalPageMetadata((await params).slug, "teams", "Drużyny")
}

export default async function PublicTeamsPage({ params }: PageProps<"/t/[slug]/teams">) {
  const { slug } = await params
  await requireSection(slug, (s) => s.showTeams)
  return <TeamsView />
}
