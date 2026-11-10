import type { Metadata } from "next"

import { FullSchedule } from "@/features/public/components/match-sections"
import { parseMatchFilters } from "@/features/public/match-filters"
import { requireSection } from "@/features/public/server"

export const metadata: Metadata = { title: "Terminarz" }

export default async function PublicSchedulePage({
  params,
  searchParams,
}: PageProps<"/t/[slug]/schedule">) {
  const { slug } = await params
  await requireSection(slug, (s) => s.showSchedule)
  return <FullSchedule initialFilters={parseMatchFilters(await searchParams)} />
}
