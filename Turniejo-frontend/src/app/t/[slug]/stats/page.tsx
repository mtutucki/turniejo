import type { Metadata } from "next"

import { StatsView } from "@/features/public/components/stats-view"
import { requireSection } from "@/features/public/server"

export const metadata: Metadata = { title: "Statystyki" }

export default async function PublicStatsPage({ params }: PageProps<"/t/[slug]/stats">) {
  const { slug } = await params
  await requireSection(slug, (s) => s.showStats)
  return <StatsView />
}
