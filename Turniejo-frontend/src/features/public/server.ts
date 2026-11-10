import "server-only"

import { notFound } from "next/navigation"
import { cache } from "react"

import type { Metadata } from "next"

import { apiFetch } from "@/lib/api/client"
import { OPEN_GRAPH_DEFAULTS } from "@/lib/site"
import { isApiError } from "@/lib/api/errors"
import type { PublicTournament, PublicTournamentListItem, PublicVisibility } from "@/types/public"
import type { MatchLivePanel } from "@/types/matches"

import { tournamentDescription } from "./seo"

/**
 * Publiczny turniej po slugu — jedno zapytanie na render (cache), wspólne dla layoutu, metadanych
 * i stron. Niepubliczny albo nieistniejący turniej to 404 (backend nie odróżnia tych przypadków).
 */
export const getPublicTournament = cache(async (slug: string) => {
  try {
    return await apiFetch<PublicTournament>(`/public/tournaments/${encodeURIComponent(slug)}`, {
      cache: "no-store",
    })
  } catch (error) {
    if (isApiError(error) && error.status === 404) notFound()
    throw error
  }
})

/** Podstrona ukryta w ustawieniach portalu też jest 404 — zakładki i tak jej nie pokazują. */
export async function requireSection(
  slug: string,
  visible: (s: PublicVisibility, t: PublicTournament) => boolean,
) {
  const tournament = await getPublicTournament(slug)
  if (!visible(tournament.settings, tournament)) notFound()
  return tournament
}

/** Lista turniejów publicznych do pierwszego renderu. Błąd backendu → undefined, lista dociągnie się w przeglądarce. */
export async function getPublicTournaments() {
  try {
    return await apiFetch<PublicTournamentListItem[]>("/public/tournaments", { cache: "no-store" })
  } catch {
    return undefined
  }
}

/** Turniej do sitemapy: niedostępny (ukryty w międzyczasie, błąd API) po prostu pomijamy. */
export async function findPublicTournament(slug: string) {
  try {
    return await apiFetch<PublicTournament>(`/public/tournaments/${encodeURIComponent(slug)}`, {
      next: { revalidate: 3600 },
    })
  } catch {
    return undefined
  }
}

/** Mecz do tytułu i podglądu linku. Mecz z innego turnieju niż w adresie traktujemy jak brak meczu. */
export const getPublicMatch = cache(async (tournamentId: string, matchId: string) => {
  try {
    const match = await apiFetch<MatchLivePanel>(
      `/public/matches/${encodeURIComponent(matchId)}/live`,
      {
        cache: "no-store",
      },
    )
    return match.tournamentId === tournamentId ? match : undefined
  } catch {
    return undefined
  }
})

/**
 * Metadane podstrony portalu: tytuł, adres kanoniczny bez filtrów (?team=…) i podgląd linku.
 * openGraph strony zastępuje cały openGraph layoutu, razem z obrazkiem z pliku opengraph-image
 * turnieju, więc opis i obrazek są i tutaj.
 */
export async function portalPageMetadata(
  slug: string,
  segment: string,
  title: string,
): Promise<Metadata> {
  const tournament = await getPublicTournament(slug)
  const path = `/t/${tournament.slug}${segment ? `/${segment}` : ""}`
  return {
    title,
    alternates: { canonical: path },
    openGraph: {
      ...OPEN_GRAPH_DEFAULTS,
      images: [{ url: `/t/${tournament.slug}/opengraph-image`, width: 1200, height: 630 }],
      title: `${title} · ${tournament.name}`,
      description: tournamentDescription(tournament),
      url: path,
    },
  }
}
