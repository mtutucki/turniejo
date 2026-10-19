"use client"

import { useQueries } from "@tanstack/react-query"
import { Timer } from "lucide-react"
import Link from "next/link"

import { EmptyState } from "@/components/layout/empty-state"
import { PageHeader } from "@/components/layout/page-header"
import { Button } from "@/components/ui/button"
import { Skeleton } from "@/components/ui/skeleton"
import { matchKeys } from "@/features/matches/api"
import { OpenMatchCard, RefereeMatchCard } from "@/features/matches/components/referee-match-card"
import { useAssignedMatches, useOpenMatches } from "@/features/matches/live-api"
import { teamKeys } from "@/features/teams/api"
import { tournamentKeys } from "@/features/tournaments/api"
import { useApi } from "@/hooks/use-api"
import type { Match, Pitch } from "@/types/matches"
import type { TournamentTeam } from "@/types/teams"
import type { Tournament } from "@/types/tournaments"

const LIVE = new Set(["Live", "Paused"])
const UPCOMING = new Set(["Scheduled", "Ready", "Postponed"])

/**
 * Lista meczów sędziego zwraca same identyfikatory drużyn i boisk — nazwy dociągamy z turniejów,
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
  description,
  matches,
  lookups,
  open = false,
}: {
  title: string
  description?: string
  matches: Match[]
  lookups: ReturnType<typeof useTournamentLookups>
  /** Mecze bez sędziego — zamiast wejścia do panelu mają przycisk przejęcia. */
  open?: boolean
}) {
  if (matches.length === 0) return null
  const MatchCard = open ? OpenMatchCard : RefereeMatchCard
  return (
    <section className="space-y-3">
      <div className="space-y-1">
        <h2 className="text-muted-foreground text-xs font-medium tracking-wide uppercase">
          {title}
        </h2>
        {description && <p className="text-muted-foreground text-sm">{description}</p>}
      </div>
      <ul className="grid gap-3 xl:grid-cols-2">
        {matches.map((match) => (
          <li key={match.id}>
            <MatchCard
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
  const openMatches = useOpenMatches()
  const all = matches.data ?? []
  const open = openMatches.data ?? []
  const tournamentIds = [...new Set([...all, ...open].map((m) => m.tournamentId))]
  const lookups = useTournamentLookups(tournamentIds)

  const live = all.filter((m) => LIVE.has(m.status))
  const upcoming = all.filter((m) => UPCOMING.has(m.status))
  const past = all
    .filter((m) => !LIVE.has(m.status) && !UPCOMING.has(m.status))
    .sort((a, b) => b.scheduledAt.localeCompare(a.scheduledAt))
  // Trwający mecz bez sędziego (ktoś go oddał w trakcie) jest pilny — idzie na samą górę.
  const openLive = open.filter((m) => LIVE.has(m.status))
  const openUpcoming = open.filter((m) => !LIVE.has(m.status))

  return (
    <div className="space-y-6">
      <PageHeader
        title="Moje mecze"
        description="Mecze, które prowadzisz jako sędzia, i wolne mecze do wzięcia."
      />
      {matches.isPending || openMatches.isPending ? (
        <div className="grid gap-3 xl:grid-cols-2">
          {[0, 1, 2].map((i) => (
            <Skeleton key={i} className="h-28 rounded-xl" />
          ))}
        </div>
      ) : all.length === 0 && open.length === 0 ? (
        <EmptyState
          icon={Timer}
          title="Nie masz meczów do sędziowania"
          description="Dołącz do turnieju kodem sędziego — zobaczysz tu mecze, które możesz wziąć, i te przydzielone przez organizatora."
          action={
            <Button asChild variant="outline">
              <Link href="/join">Dołącz kodem</Link>
            </Button>
          }
        />
      ) : (
        <>
          <Section
            title="Trwają bez sędziego"
            description="Te mecze już się toczą i nikt ich nie prowadzi."
            matches={openLive}
            lookups={lookups}
            open
          />
          <Section title="Teraz" matches={live} lookups={lookups} />
          <Section title="Nadchodzące" matches={upcoming} lookups={lookups} />
          <Section
            title="Wolne mecze"
            description="Nie mają jeszcze sędziego — weź ten, który poprowadzisz."
            matches={openUpcoming}
            lookups={lookups}
            open
          />
          <Section title="Zakończone" matches={past} lookups={lookups} />
        </>
      )}
    </div>
  )
}
