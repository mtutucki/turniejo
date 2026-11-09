import type { Metadata } from "next"

import { DemoBanner } from "@/components/demo-banner"
import { LegalLinks } from "@/components/layout/legal-links"
import { ChampionBanner } from "@/features/public/components/champion-banner"
import { PublicHeader } from "@/features/public/components/public-header"
import { PublicTournamentProvider } from "@/features/public/public-context"
import { getPublicTournament } from "@/features/public/server"
import { formatVersion } from "@/lib/version"

export async function generateMetadata({ params }: LayoutProps<"/t/[slug]">): Promise<Metadata> {
  const { slug } = await params
  const tournament = await getPublicTournament(slug)
  return {
    title: { default: tournament.name, template: `%s · ${tournament.name}` },
    description:
      tournament.description ?? `Wyniki, terminarz i tabele turnieju ${tournament.name}.`,
  }
}

// Portal kibica: bez logowania i bez powłoki aplikacji. Niepubliczny turniej → 404 (getPublicTournament).
export default async function PublicTournamentLayout({
  children,
  params,
}: LayoutProps<"/t/[slug]">) {
  const { slug } = await params
  const tournament = await getPublicTournament(slug)
  const version = formatVersion()

  return (
    <PublicTournamentProvider initialTournament={tournament}>
      <div className="flex min-h-dvh flex-1 flex-col">
        <DemoBanner />
        <PublicHeader />
        <main className="mx-auto w-full max-w-5xl flex-1 px-4 py-6">
          <ChampionBanner />
          {children}
        </main>
        <footer className="text-muted-foreground space-y-2 border-t py-6 text-center text-xs">
          <p>
            Wyniki na żywo · Turniejo
            {version && <span title={version.details}> · wersja {version.label}</span>}
          </p>
          <LegalLinks className="justify-center" />
        </footer>
      </div>
    </PublicTournamentProvider>
  )
}
