import type { Metadata } from "next"

import { BracketView } from "@/features/public/components/bracket-view"
import { requireSection } from "@/features/public/server"

export const metadata: Metadata = { title: "Drabinka" }

export default async function PublicBracketPage({ params }: PageProps<"/t/[slug]/bracket">) {
  const { slug } = await params
  await requireSection(
    slug,
    (s, t) => s.showBracket && (t.format === "Knockout" || t.format === "GroupAndKnockout"),
  )
  return <BracketView />
}
