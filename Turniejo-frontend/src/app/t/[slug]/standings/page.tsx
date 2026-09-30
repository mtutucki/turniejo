import type { Metadata } from "next"

import { StandingsView } from "@/features/public/components/standings-view"
import { requireSection } from "@/features/public/server"

export const metadata: Metadata = { title: "Tabele" }

export default async function PublicStandingsPage({ params }: PageProps<"/t/[slug]/standings">) {
  const { slug } = await params
  await requireSection(slug, (s) => s.showStandings)
  return <StandingsView />
}
