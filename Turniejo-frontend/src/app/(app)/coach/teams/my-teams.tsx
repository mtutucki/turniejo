"use client"

import { ChevronRight, Lock, Shirt } from "lucide-react"
import Link from "next/link"

import { EmptyState } from "@/components/layout/empty-state"
import { PageHeader } from "@/components/layout/page-header"
import { Card, CardContent } from "@/components/ui/card"
import { Skeleton } from "@/components/ui/skeleton"
import { panelKeys, useMyTeams } from "@/features/panels/api"
import { TeamLogo } from "@/features/teams/components/team-logo"
import { TournamentStatusBadge } from "@/features/tournaments/components/status-badge"
import { prefetchHandlers, usePrefetch } from "@/hooks/use-prefetch"
import { TEAM_STAFF_ROLE_LABELS } from "@/types/teams"

export function MyTeamsView() {
  const teams = useMyTeams()
  const prefetch = usePrefetch()
  return (
    <div>
      <PageHeader
        title="Moje drużyny"
        description="Skład, terminarz i statystyki drużyn, które prowadzisz."
      />
      {teams.isPending ? (
        <div className="grid gap-3 sm:grid-cols-2">
          {[0, 1].map((i) => (
            <Skeleton key={i} className="h-24 rounded-xl" />
          ))}
        </div>
      ) : teams.data?.length ? (
        <ul className="grid gap-3 sm:grid-cols-2">
          {teams.data.map((team) => (
            <li key={`${team.tournamentTeamId}-${team.role}`}>
              <Link
                href={`/coach/teams/${team.tournamentTeamId}/players`}
                {...prefetchHandlers(() =>
                  prefetch({
                    queryKey: panelKeys.team(team.tournamentTeamId, "players"),
                    path: `/api/coach/teams/${team.tournamentTeamId}/players`,
                  }),
                )}
                className="focus-visible:ring-ring block rounded-xl outline-none focus-visible:ring-2"
              >
                <Card className="hover:border-primary/50 h-full py-4 transition-colors">
                  <CardContent className="flex items-center gap-4 px-4">
                    <TeamLogo name={team.teamName} logoUrl={null} size={44} />
                    <div className="min-w-0 flex-1 space-y-1">
                      <p className="truncate font-semibold">{team.teamName}</p>
                      <p className="text-muted-foreground truncate text-xs">
                        {team.tournamentName} · {TEAM_STAFF_ROLE_LABELS[team.role]}
                      </p>
                      <div className="flex flex-wrap items-center gap-2">
                        {team.tournamentStatus && (
                          <TournamentStatusBadge status={team.tournamentStatus} />
                        )}
                        {!team.canEditSquad && (
                          <span className="text-muted-foreground inline-flex items-center gap-1 text-xs">
                            <Lock className="size-3" />
                            Skład zablokowany
                          </span>
                        )}
                      </div>
                    </div>
                    <ChevronRight className="text-muted-foreground size-5 shrink-0" />
                  </CardContent>
                </Card>
              </Link>
            </li>
          ))}
        </ul>
      ) : (
        <EmptyState
          icon={Shirt}
          title="Nie prowadzisz żadnej drużyny"
          description="Organizator turnieju przypisze Cię do sztabu drużyny."
        />
      )}
    </div>
  )
}
