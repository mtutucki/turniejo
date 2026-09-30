import type { Metadata } from "next"

import { FullSchedule } from "@/features/public/components/match-sections"
import { requireSection } from "@/features/public/server"

export const metadata: Metadata = { title: "Terminarz" }

export default async function PublicSchedulePage({ params }: PageProps<"/t/[slug]/schedule">) {
  const { slug } = await params
  await requireSection(slug, (s) => s.showSchedule)
  return <FullSchedule />
}
