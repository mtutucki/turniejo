import type { Metadata } from "next"

import { TeamsView } from "@/features/public/components/teams-view"
import { requireSection } from "@/features/public/server"

export const metadata: Metadata = { title: "Drużyny" }

export default async function PublicTeamsPage({ params }: PageProps<"/t/[slug]/teams">) {
  const { slug } = await params
  await requireSection(slug, (s) => s.showTeams)
  return <TeamsView />
}
