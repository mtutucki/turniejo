import type { Metadata } from "next"

import { DemoBanner } from "@/components/demo-banner"
import { LegalLinks } from "@/components/layout/legal-links"
import { ChampionBanner } from "@/features/public/components/champion-banner"
import { PublicHeader } from "@/features/public/components/public-header"
import { PublicTournamentProvider } from "@/features/public/public-context"
import { jsonLdScript, tournamentDescription, tournamentJsonLd } from "@/features/public/seo"
import { getPublicTournament } from "@/features/public/server"
import { OPEN_GRAPH_DEFAULTS } from "@/lib/site"
import { formatVersion } from "@/lib/version"

export async function generateMetadata({ params }: LayoutProps<"/t/[slug]">): Promise<Metadata> {
  const { slug } = await params
  const tournament = await getPublicTournament(slug)
  const description = tournamentDescription(tournament)
  // Turniej ukryty z listy turniejów organizator udostępnia samym linkiem: bez Google, ale
  // podgląd linku na Messengerze czy WhatsAppie zostaje.
  const listed = tournament.settings.showInDirectory !== false
  return {
    title: { default: tournament.name, template: `%s · ${tournament.name}` },
    description,
    robots: listed ? undefined : { index: false, follow: false },
    openGraph: {
      ...OPEN_GRAPH_DEFAULTS,
      title: tournament.name,
      description,
      url: `/t/${tournament.slug}`,
    },
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
      <script
        type="application/ld+json"
        dangerouslySetInnerHTML={{ __html: jsonLdScript(tournamentJsonLd(tournament)) }}
      />
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
