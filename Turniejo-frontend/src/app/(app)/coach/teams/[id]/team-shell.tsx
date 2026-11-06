"use client"

import { ArrowLeft, ShieldAlert } from "lucide-react"
import Link from "next/link"
import { useParams } from "next/navigation"
import { createContext, type ReactNode, useContext } from "react"

import { EmptyState } from "@/components/layout/empty-state"
import { Button } from "@/components/ui/button"
import { Skeleton } from "@/components/ui/skeleton"
import { useMyTeams } from "@/features/panels/api"
import { SectionTabs } from "@/features/panels/components/section-tabs"
import { TeamLogo } from "@/features/teams/components/team-logo"
import { TournamentStatusBadge } from "@/features/tournaments/components/status-badge"
import type { MyTeam } from "@/types/roles-panels"
import { TEAM_STAFF_ROLE_LABELS } from "@/types/teams"

const TABS = [
  { segment: "players", label: "Skład" },
  { segment: "roster", label: "Kadra" },
  { segment: "schedule", label: "Terminarz" },
  { segment: "stats", label: "Statystyki" },
]

const CoachTeamContext = createContext<MyTeam | null>(null)

/** Drużyna z /api/coach/teams — dostępna tylko w podstronach /coach/teams/[id]. */
export function useCoachTeam() {
  const team = useContext(CoachTeamContext)
  if (!team) throw new Error("useCoachTeam poza TeamShell")
  return team
}

export function TeamShell({ children }: { children: ReactNode }) {
  const { id } = useParams<{ id: string }>()
  const teams = useMyTeams()
  // Ta sama drużyna może mieć kilka wpisów (np. trener i kierownik) — trener ma pierwszeństwo.
  const matching = teams.data?.filter((t) => t.tournamentTeamId === id) ?? []
  const team = matching.find((t) => t.role === "Coach") ?? matching[0]

  const back = (
    <Button asChild variant="ghost" size="sm" className="-ml-2">
      <Link href="/coach/teams">
        <ArrowLeft />
        Moje drużyny
      </Link>
    </Button>
  )

  if (teams.isPending) return <Skeleton className="h-64 w-full rounded-xl" />
  if (!team) {
    return (
      <div className="space-y-4">
        {back}
        <EmptyState
          icon={ShieldAlert}
          title="Brak dostępu do tej drużyny"
          description="Widzisz tylko drużyny, w których sztabie jesteś."
        />
      </div>
    )
  }

  return (
    <CoachTeamContext.Provider value={team}>
      <div className="space-y-6">
        <header className="space-y-3">
          {back}
          <div className="flex flex-wrap items-center gap-3">
            <TeamLogo name={team.teamName} logoUrl={null} size={48} />
            <div className="min-w-0">
              <h1 className="truncate text-2xl font-semibold tracking-tight">{team.teamName}</h1>
              <p className="text-muted-foreground flex flex-wrap items-center gap-2 text-sm">
                {team.tournamentName} · {TEAM_STAFF_ROLE_LABELS[team.role]}
                {team.tournamentStatus && <TournamentStatusBadge status={team.tournamentStatus} />}
              </p>
            </div>
          </div>
          <SectionTabs base={`/coach/teams/${id}`} tabs={TABS} />
        </header>
        {children}
      </div>
    </CoachTeamContext.Provider>
  )
}
