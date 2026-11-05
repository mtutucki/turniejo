"use client"

import { useTournamentRanking } from "@/features/matches/api"
import { PLAYER_RANKINGS, RankingCard } from "@/features/matches/components/player-rankings"
import { useTournamentTeams } from "@/features/teams/api"
import { useTournamentContext } from "@/features/tournaments/use-tournament-context"

function TournamentRankingCard({
  kind,
  teamName,
  ...props
}: (typeof PLAYER_RANKINGS)[number] & { teamName: (tournamentTeamId: string) => string | null }) {
  const { id } = useTournamentContext()
  const ranking = useTournamentRanking(id, kind)
  return <RankingCard {...props} ranking={ranking} teamName={teamName} />
}

/** Statystyki zawodników: te same rankingi co w portalu publicznym, ale z pełnymi nazwiskami. */
export function TournamentStatsView() {
  const { id } = useTournamentContext()
  const teams = useTournamentTeams(id)
  const teamById = new Map((teams.data ?? []).map((t) => [t.id, t.teamName]))
  const teamName = (tournamentTeamId: string) => teamById.get(tournamentTeamId) ?? null

  return (
    <div className="space-y-4">
      <div className="grid gap-4 lg:grid-cols-2">
        {PLAYER_RANKINGS.map((r) => (
          <TournamentRankingCard key={r.kind} {...r} teamName={teamName} />
        ))}
      </div>
      <p className="text-muted-foreground text-xs">
        Liczą się gole i asysty przypisane do zawodnika. Gole wpisane bez strzelca nie trafiają do
        rankingu, dopóki nie dopiszesz strzelca w panelu meczu.
      </p>
    </div>
  )
}
