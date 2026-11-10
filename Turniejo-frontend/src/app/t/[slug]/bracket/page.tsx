import type { Metadata } from "next"

import { BracketView } from "@/features/public/components/bracket-view"
import { portalPageMetadata, requireSection } from "@/features/public/server"

export async function generateMetadata({
  params,
}: PageProps<"/t/[slug]/bracket">): Promise<Metadata> {
  return portalPageMetadata((await params).slug, "bracket", "Drabinka")
}

export default async function PublicBracketPage({ params }: PageProps<"/t/[slug]/bracket">) {
  const { slug } = await params
  await requireSection(
    slug,
    (s, t) => s.showBracket && (t.format === "Knockout" || t.format === "GroupAndKnockout"),
  )
  return <BracketView />
}
