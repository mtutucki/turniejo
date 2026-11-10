import type { Metadata } from "next"

import { StatsView } from "@/features/public/components/stats-view"
import { portalPageMetadata, requireSection } from "@/features/public/server"

export async function generateMetadata({
  params,
}: PageProps<"/t/[slug]/stats">): Promise<Metadata> {
  return portalPageMetadata((await params).slug, "stats", "Statystyki")
}

export default async function PublicStatsPage({ params }: PageProps<"/t/[slug]/stats">) {
  const { slug } = await params
  await requireSection(slug, (s) => s.showStats)
  return <StatsView />
}
