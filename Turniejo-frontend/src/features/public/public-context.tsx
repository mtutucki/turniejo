"use client"

import { createContext, type ReactNode, useContext, useMemo } from "react"

import type { RealtimeState } from "@/features/matches/use-match-realtime"
import type { PublicTeam, PublicTournament } from "@/types/public"

import { usePublicTeams, usePublicTournament } from "./api"
import { useTournamentRealtime } from "./use-tournament-realtime"

type Lookups = {
  teamName: (tournamentTeamId: string | null, fallback?: string) => string
  teamShortName: (tournamentTeamId: string | null) => string | null
  groupName: (groupId: string | null) => string | null
  categoryName: (categoryId: string | null) => string | null
  pitchName: (pitchId: string | null) => string | null
}

type PublicContextValue = {
  slug: string
  tournament: PublicTournament
  teams: PublicTeam[]
  lookups: Lookups
  realtime: RealtimeState
}

// Stała referencja — nowa [] przy każdym renderze unieważniałaby useMemo z lookupami.
const NO_TEAMS: PublicTeam[] = []

const PublicContext = createContext<PublicContextValue | null>(null)

export function PublicTournamentProvider({
  initialTournament,
  children,
}: {
  initialTournament: PublicTournament
  children: ReactNode
}) {
  const slug = initialTournament.slug
  const tournament = usePublicTournament(slug, initialTournament).data ?? initialTournament
  const teams = usePublicTeams(slug, tournament.settings.showTeams).data ?? NO_TEAMS
  const realtime = useTournamentRealtime(slug, tournament.id)

  const lookups = useMemo<Lookups>(() => {
    const teamById = new Map(teams.map((t) => [t.tournamentTeamId, t]))
    const groupById = new Map(tournament.groups.map((g) => [g.id, g.name]))
    const categoryById = new Map(tournament.categories.map((c) => [c.id, c.name]))
    const pitchById = new Map(tournament.pitches.map((p) => [p.id, p.name]))
    return {
      // ShowTeams=false → backend nie zdradza nazw drużyn, pokazujemy neutralne etykiety.
      teamName: (id, fallback = "Drużyna") => (id && teamById.get(id)?.name) || fallback,
      teamShortName: (id) => (id && teamById.get(id)?.shortName) || null,
      groupName: (id) => (id ? (groupById.get(id) ?? null) : null),
      categoryName: (id) => (id ? (categoryById.get(id) ?? null) : null),
      pitchName: (id) => (id ? (pitchById.get(id) ?? null) : null),
    }
  }, [teams, tournament])

  return (
    <PublicContext.Provider value={{ slug, tournament, teams, lookups, realtime }}>
      {children}
    </PublicContext.Provider>
  )
}

export function usePublicContext() {
  const value = useContext(PublicContext)
  if (!value) throw new Error("usePublicContext poza PublicTournamentProvider")
  return value
}
