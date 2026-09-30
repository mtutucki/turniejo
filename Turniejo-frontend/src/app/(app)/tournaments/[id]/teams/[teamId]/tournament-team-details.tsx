"use client"

import { ArrowLeft, SearchX, UserPlus, Users } from "lucide-react"
import Link from "next/link"
import { useParams } from "next/navigation"

import { EmptyState } from "@/components/layout/empty-state"
import { Button } from "@/components/ui/button"
import {
  Card,
  CardAction,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/card"
import { Skeleton } from "@/components/ui/skeleton"
import { useTeamPlayers, useTeamStaff, useTournamentTeams } from "@/features/teams/api"
import { AddPlayerDialog } from "@/features/teams/components/add-player-dialog"
import { AssignGuardianDialog } from "@/features/teams/components/assign-guardian-dialog"
import { AssignStaffDialog } from "@/features/teams/components/assign-staff-dialog"
import { RegistrationStatusBadge } from "@/features/teams/components/registration-status-badge"
import { TeamLogo } from "@/features/teams/components/team-logo"
import { useCategories } from "@/features/tournaments/api"
import { useTournamentContext } from "@/features/tournaments/use-tournament-context"
import { useMyRoles } from "@/hooks/use-my-roles"
import { plural } from "@/lib/format"
import { UserRole } from "@/types/roles"
import {
  PLAYER_POSITION_LABELS,
  PLAYER_STATUS_LABELS,
  TEAM_STAFF_ROLE_LABELS,
  type TournamentPlayer,
} from "@/types/teams"

function Squad({
  players,
  isPending,
  guardianScope,
}: {
  players: TournamentPlayer[] | undefined
  isPending: boolean
  /** Tylko organizator — lista członków turnieju (wybór rodzica) jest dla niego zarezerwowana. */
  guardianScope?: { tournamentId: string; tournamentTeamId: string }
}) {
  if (isPending) return <Skeleton className="h-32" />
  if (!players?.length) {
    return (
      <EmptyState icon={Users} title="Skład jest pusty" description="Dodaj pierwszego zawodnika." />
    )
  }
  return (
    <ul className="divide-y">
      {players.map((p) => (
        <li key={p.id} className="flex items-center gap-3 py-2 text-sm">
          <span className="text-primary w-8 text-right font-mono tabular-nums">
            {p.shirtNumber ?? "–"}
          </span>
          <span className="min-w-0 flex-1">
            <span className="block truncate font-medium">
              {p.firstName} {p.lastName}
            </span>
            <span className="text-muted-foreground text-xs">
              {p.birthDate && (
                <span className="font-mono tabular-nums">{p.birthDate.slice(0, 4)}</span>
              )}
              {p.position && ` · ${PLAYER_POSITION_LABELS[p.position]}`}
            </span>
          </span>
          {p.status !== "Active" && (
            <span className="text-muted-foreground text-xs">{PLAYER_STATUS_LABELS[p.status]}</span>
          )}
          {guardianScope && <AssignGuardianDialog {...guardianScope} player={p} />}
        </li>
      ))}
    </ul>
  )
}

export function TournamentTeamDetails() {
  const { teamId } = useParams<{ teamId: string }>()
  const { id, isOrganizer } = useTournamentContext()
  const teams = useTournamentTeams(id)
  const categories = useCategories(id)
  const team = teams.data?.find((t) => t.id === teamId)
  const { hasAnyRole: hasTeamRole } = useMyRoles({
    resourceType: "TournamentTeam",
    resourceId: teamId,
  })
  const isTeamStaff = hasTeamRole([UserRole.Coach, UserRole.AssistantCoach, UserRole.Manager])
  // Skład edytuje organizator albo trener tej drużyny (AssignPlayerToTournamentTeamCommand).
  const canEditSquad = isOrganizer || hasTeamRole([UserRole.Coach])
  const players = useTeamPlayers(id, teamId)
  const staff = useTeamStaff(id, teamId, isOrganizer || isTeamStaff)

  if (teams.isPending) return <Skeleton className="h-64 rounded-xl" />
  if (!team) {
    return (
      <EmptyState
        icon={SearchX}
        title="Nie znaleziono drużyny"
        action={
          <Button asChild variant="outline">
            <Link href={`/tournaments/${id}/teams`}>Wróć do drużyn</Link>
          </Button>
        }
      />
    )
  }

  const category = categories.data?.find((c) => c.id === team.categoryId)

  return (
    <div className="space-y-6">
      <div className="space-y-3">
        <Link
          href={`/tournaments/${id}/teams`}
          className="text-muted-foreground hover:text-foreground inline-flex items-center gap-1 text-sm"
        >
          <ArrowLeft className="size-4" />
          Drużyny
        </Link>
        <div className="flex flex-wrap items-center gap-3">
          <TeamLogo name={team.teamName} logoUrl={team.teamLogoUrl} size={48} />
          <div>
            <h2 className="text-xl font-semibold">{team.teamName}</h2>
            <p className="text-muted-foreground flex items-center gap-2 text-sm">
              {category?.name}
              <RegistrationStatusBadge status={team.registrationStatus} />
            </p>
          </div>
        </div>
      </div>

      <div className="grid gap-6 xl:grid-cols-[2fr_1fr]">
        <Card>
          <CardHeader>
            <CardTitle className="text-base">Skład</CardTitle>
            <CardDescription>
              {plural(players.data?.length ?? 0, ["zawodnik", "zawodników", "zawodników"])}
            </CardDescription>
            {canEditSquad && (
              <CardAction>
                <AddPlayerDialog
                  tournamentId={id}
                  tournamentTeamId={teamId}
                  category={category}
                  squad={players.data ?? []}
                />
              </CardAction>
            )}
          </CardHeader>
          <CardContent>
            <Squad
              players={players.data}
              isPending={players.isPending}
              guardianScope={
                isOrganizer ? { tournamentId: id, tournamentTeamId: teamId } : undefined
              }
            />
          </CardContent>
        </Card>

        {(isOrganizer || isTeamStaff) && (
          <Card>
            <CardHeader>
              <CardTitle className="text-base">Sztab</CardTitle>
              {isOrganizer && (
                <CardAction>
                  <AssignStaffDialog
                    tournamentId={id}
                    tournamentTeamId={teamId}
                    staff={staff.data ?? []}
                    trigger={
                      <Button size="sm" variant="outline">
                        <UserPlus />
                        Przypisz
                      </Button>
                    }
                  />
                </CardAction>
              )}
            </CardHeader>
            <CardContent>
              {staff.isPending ? (
                <Skeleton className="h-16" />
              ) : staff.data?.length ? (
                <ul className="divide-y">
                  {staff.data.map((s) => (
                    <li key={s.id} className="py-2 text-sm">
                      <p className="font-medium">
                        {s.firstName || s.lastName
                          ? `${s.firstName ?? ""} ${s.lastName ?? ""}`
                          : "—"}
                      </p>
                      <p className="text-muted-foreground text-xs">
                        {TEAM_STAFF_ROLE_LABELS[s.role]}
                        {s.email && ` · ${s.email}`}
                      </p>
                    </li>
                  ))}
                </ul>
              ) : (
                <p className="text-muted-foreground text-sm">Brak przypisanego sztabu.</p>
              )}
            </CardContent>
          </Card>
        )}
      </div>
    </div>
  )
}
