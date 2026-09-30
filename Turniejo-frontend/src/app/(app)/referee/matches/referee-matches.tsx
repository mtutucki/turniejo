"use client"

import { useQueries } from "@tanstack/react-query"
import { Timer } from "lucide-react"
import Link from "next/link"

import { EmptyState } from "@/components/layout/empty-state"
import { PageHeader } from "@/components/layout/page-header"
import { Button } from "@/components/ui/button"
import { Skeleton } from "@/components/ui/skeleton"
import { matchKeys } from "@/features/matches/api"
import { RefereeMatchCard } from "@/features/matches/components/referee-match-card"
import { useAssignedMatches } from "@/features/matches/live-api"
import { teamKeys } from "@/features/teams/api"
import { tournamentKeys } from "@/features/tournaments/api"
import { useApi } from "@/hooks/use-api"
import type { Match, Pitch } from "@/types/matches"
import type { TournamentTeam } from "@/types/teams"
import type { Tournament } from "@/types/tournaments"

const LIVE = new Set(["Live", "Paused"])
const UPCOMING = new Set(["Scheduled", "Ready", "Postponed"])

/**
 * GetAssignedMatchesQuery zwraca same identyfikatory drużyn i boisk — nazwy dociągamy z turniejów,
 * w których sędziuje (sędzia jest ich członkiem, więc ma do nich dostęp). Te same klucze co reszta
 * aplikacji, więc dane dzielą cache z widokami turnieju.
 */
function useTournamentLookups(tournamentIds: string[]) {
  const { request, isAuthenticated } = useApi()
  const teams = useQueries({
    queries: tournamentIds.map((id) => ({
      queryKey: teamKeys.tournamentTeams(id),
      queryFn: () => request<TournamentTeam[]>(`/api/tournaments/${id}/teams`),
      enabled: isAuthenticated,
    })),
  })
  const pitches = useQueries({
    queries: tournamentIds.map((id) => ({
      queryKey: matchKeys.pitches(id),
      queryFn: () => request<Pitch[]>(`/api/tournaments/${id}/pitches`),
      enabled: isAuthenticated,
    })),
  })
  const tournaments = useQueries({
    queries: tournamentIds.map((id) => ({
      queryKey: tournamentKeys.detail(id),
      queryFn: () => request<Tournament>(`/api/tournaments/${id}`),
      enabled: isAuthenticated,
    })),
  })

  const teamNames = new Map(teams.flatMap((q) => q.data ?? []).map((t) => [t.id, t.teamName]))
  const pitchNames = new Map(pitches.flatMap((q) => q.data ?? []).map((p) => [p.id, p.name]))
  const tournamentNames = new Map(
    tournaments.flatMap((q) => (q.data ? [[q.data.id, q.data.name] as const] : [])),
  )
  return { teamNames, pitchNames, tournamentNames }
}

function Section({
  title,
  matches,
  lookups,
}: {
  title: string
  matches: Match[]
  lookups: ReturnType<typeof useTournamentLookups>
}) {
  if (matches.length === 0) return null
  return (
    <section className="space-y-3">
      <h2 className="text-muted-foreground text-xs font-medium tracking-wide uppercase">{title}</h2>
      <ul className="grid gap-3 xl:grid-cols-2">
        {matches.map((match) => (
          <li key={match.id}>
            <RefereeMatchCard
              match={match}
              homeName={lookups.teamNames.get(match.homeTournamentTeamId) ?? "Drużyna"}
              awayName={lookups.teamNames.get(match.awayTournamentTeamId) ?? "Drużyna"}
              pitchName={match.pitchId ? (lookups.pitchNames.get(match.pitchId) ?? "Boisko") : null}
              tournamentName={lookups.tournamentNames.get(match.tournamentId) ?? null}
            />
          </li>
        ))}
      </ul>
    </section>
  )
}

export function RefereeMatchesView() {
  const matches = useAssignedMatches()
  const all = matches.data ?? []
  const tournamentIds = [...new Set(all.map((m) => m.tournamentId))]
  const lookups = useTournamentLookups(tournamentIds)

  const live = all.filter((m) => LIVE.has(m.status))
  const upcoming = all.filter((m) => UPCOMING.has(m.status))
  const past = all
    .filter((m) => !LIVE.has(m.status) && !UPCOMING.has(m.status))
    .sort((a, b) => b.scheduledAt.localeCompare(a.scheduledAt))

  return (
    <div className="space-y-6">
      <PageHeader title="Moje mecze" description="Mecze, które prowadzisz jako sędzia." />
      {matches.isPending ? (
        <div className="grid gap-3 xl:grid-cols-2">
          {[0, 1, 2].map((i) => (
            <Skeleton key={i} className="h-28 rounded-xl" />
          ))}
        </div>
      ) : all.length === 0 ? (
        <EmptyState
          icon={Timer}
          title="Nie masz przypisanych meczów"
          description="Dołącz do turnieju kodem sędziego — organizator przypisze Cię do meczów."
          action={
            <Button asChild variant="outline">
              <Link href="/join">Dołącz kodem</Link>
            </Button>
          }
        />
      ) : (
        <>
          <Section title="Teraz" matches={live} lookups={lookups} />
          <Section title="Nadchodzące" matches={upcoming} lookups={lookups} />
          <Section title="Zakończone" matches={past} lookups={lookups} />
        </>
      )}
    </div>
  )
}
