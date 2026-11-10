import type { MetadataRoute } from "next"

import {
  findPublicTeams,
  findPublicTournament,
  getPublicTournaments,
} from "@/features/public/server"
import { publicSections } from "@/features/public/sections"
import { absoluteUrl } from "@/lib/site"

// Odświeżana co godzinę: nowe turnieje trafiają do Google bez nowego wdrożenia. Przy budowaniu API
// zwykle nie działa, wtedy sitemapa ma same stałe strony do pierwszego odświeżenia.
export const revalidate = 3600

const STATIC_PAGES = ["/", "/help", "/changelog", "/terms", "/privacy"]

export default async function sitemap(): Promise<MetadataRoute.Sitemap> {
  const pages: MetadataRoute.Sitemap = STATIC_PAGES.map((path) => ({
    url: absoluteUrl(path),
    changeFrequency: path === "/" ? "hourly" : "monthly",
    priority: path === "/" ? 1 : 0.3,
  }))

  // Tylko turnieje z listy publicznej: ukryte organizator udostępnia samym linkiem.
  const listed = (await getPublicTournaments()) ?? []
  const tournaments = await Promise.all(listed.map((item) => findPublicTournament(item.slug)))

  for (const [i, tournament] of tournaments.entries()) {
    if (!tournament) continue
    const item = listed[i]!
    const live = item.liveMatchCount > 0 || item.status === "InProgress"
    const finished = item.status === "Finished"
    const changeFrequency = live ? "always" : finished ? "monthly" : "daily"
    for (const { segment } of publicSections(tournament)) {
      pages.push({
        url: absoluteUrl(`/t/${tournament.slug}${segment ? `/${segment}` : ""}`),
        changeFrequency,
        priority: segment ? 0.6 : 0.8,
      })
    }
    // Strony drużyn: tylko przy „Pokazuj drużyny” (bez tego portal nie zna nazw drużyn).
    if (tournament.settings.showTeams) {
      for (const team of (await findPublicTeams(tournament.slug)) ?? []) {
        pages.push({
          url: absoluteUrl(`/t/${tournament.slug}/teams/${team.tournamentTeamId}`),
          changeFrequency,
          priority: 0.5,
        })
      }
    }
  }

  return pages
}
