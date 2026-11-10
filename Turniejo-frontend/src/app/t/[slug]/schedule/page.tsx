import type { Metadata } from "next"

import { FullSchedule } from "@/features/public/components/match-sections"
import { parseMatchFilters } from "@/features/public/match-filters"
import { portalPageMetadata, requireSection } from "@/features/public/server"

export async function generateMetadata({
  params,
}: PageProps<"/t/[slug]/schedule">): Promise<Metadata> {
  return portalPageMetadata((await params).slug, "schedule", "Terminarz")
}

export default async function PublicSchedulePage({
  params,
  searchParams,
}: PageProps<"/t/[slug]/schedule">) {
  const { slug } = await params
  await requireSection(slug, (s) => s.showSchedule)
  return <FullSchedule initialFilters={parseMatchFilters(await searchParams)} />
}
