import "server-only"

import { notFound } from "next/navigation"
import { cache } from "react"

import { apiFetch } from "@/lib/api/client"
import { isApiError } from "@/lib/api/errors"
import type { PublicTournament, PublicVisibility } from "@/types/public"

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
