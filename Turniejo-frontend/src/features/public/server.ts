import "server-only"

import type { Metadata } from "next"
import { notFound } from "next/navigation"
import { cache } from "react"

import { apiFetch } from "@/lib/api/client"
import { isApiError } from "@/lib/api/errors"
import { OPEN_GRAPH_DEFAULTS } from "@/lib/site"
import type { MatchLivePanel } from "@/types/matches"
import type {
  PublicTeam,
  PublicTournament,
  PublicTournamentListItem,
  PublicVisibility,
} from "@/types/public"

import { tournamentDescription } from "./seo"

/**
 * Slug z `params` strony. Next oddaje parametr trasy z zakodowanymi znakami takimi jak `:`
 * (`8%3A00`), a encodeURIComponent zakodowałby je drugi raz i backend zwróciłby 404.
 * Stare slugi mają takie znaki (godzina w nazwie turnieju).
 */
export function routeSlug(slug: string) {
  try {
    return decodeURIComponent(slug)
  } catch {
    return slug
  }
}

/**
 * Publiczny turniej po slugu z adresu strony: jedno zapytanie na render (cache), wspólne dla layoutu,
 * metadanych i stron. Niepubliczny albo nieistniejący turniej to 404 (backend nie odróżnia tych przypadków).
 */
export const getPublicTournament = cache(async (slug: string) => {
  try {
    return await apiFetch<PublicTournament>(
      `/public/tournaments/${encodeURIComponent(routeSlug(slug))}`,
      {
        cache: "no-store",
      },
    )
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

/** Drużyna turnieju do tytułu strony drużyny (slug z adresu strony). Niewidoczna (ukryte drużyny, wycofana) → 404. */
export const getPublicTeam = cache(async (slug: string, teamId: string) => {
  try {
    return await apiFetch<PublicTeam>(
      `/public/tournaments/${encodeURIComponent(routeSlug(slug))}/teams/${encodeURIComponent(teamId)}`,
      { cache: "no-store" },
    )
  } catch (error) {
    if (isApiError(error) && (error.status === 404 || error.status === 400)) notFound()
    throw error
  }
})

/** Drużyny turnieju do sitemapy. Błąd API → brak stron drużyn w tym odświeżeniu. */
export async function findPublicTeams(slug: string) {
  try {
    return await apiFetch<PublicTeam[]>(`/public/tournaments/${encodeURIComponent(slug)}/teams`, {
      next: { revalidate: 3600 },
    })
  } catch {
    return undefined
  }
}
