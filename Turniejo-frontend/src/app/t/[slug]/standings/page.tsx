import type { Metadata } from "next"

import { StandingsView } from "@/features/public/components/standings-view"
import { portalPageMetadata, requireSection } from "@/features/public/server"

export async function generateMetadata({
  params,
}: PageProps<"/t/[slug]/standings">): Promise<Metadata> {
  return portalPageMetadata((await params).slug, "standings", "Tabele")
}

export default async function PublicStandingsPage({ params }: PageProps<"/t/[slug]/standings">) {
  const { slug } = await params
  await requireSection(slug, (s, t) => s.showStandings && t.format !== "Knockout")
  return <StandingsView />
}
