"use client"

import { ArrowLeft } from "lucide-react"
import Link from "next/link"
import { useParams } from "next/navigation"

import { Button } from "@/components/ui/button"
import { useOrganizationTeams, useTeamRoster } from "@/features/teams/api"
import { TeamHistorySection } from "@/features/teams/components/team-history-section"
import { TeamLogo } from "@/features/teams/components/team-logo"
import { TeamRoster } from "@/features/teams/components/team-roster"

export function TeamRosterPage() {
  const { id, teamId } = useParams<{ id: string; teamId: string }>()
  const teams = useOrganizationTeams(id)
  const roster = useTeamRoster(teamId)
  const team = teams.data?.find((t) => t.id === teamId)
  const name = team?.name ?? roster.data?.teamName ?? "Drużyna"

  return (
    <div className="space-y-6">
      <div className="space-y-3">
        <Button asChild variant="ghost" size="sm" className="-ml-2">
          <Link href={`/organizations/${id}/teams`}>
            <ArrowLeft />
            Baza drużyn
          </Link>
        </Button>
        <div className="flex items-center gap-3">
          <TeamLogo name={name} logoUrl={team?.logoUrl ?? null} size={48} />
          <div className="min-w-0">
            <h1 className="truncate text-2xl font-semibold tracking-tight">{name}</h1>
            <p className="text-muted-foreground text-sm">
              Statystyki ze wszystkich turniejów i kadra, z której wybierasz skład na turniej.
            </p>
          </div>
        </div>
      </div>
      <TeamHistorySection teamId={teamId} />
      <TeamRoster teamId={teamId} />
    </div>
  )
}
