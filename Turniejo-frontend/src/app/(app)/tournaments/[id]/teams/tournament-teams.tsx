"use client"

import { Layers, Plus, Shield, UserPlus } from "lucide-react"
import Link from "next/link"

import { EmptyState } from "@/components/layout/empty-state"
import { Button } from "@/components/ui/button"
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card"
import { Skeleton } from "@/components/ui/skeleton"
import { useTournamentTeams } from "@/features/teams/api"
import { AddTeamDialog } from "@/features/teams/components/add-team-dialog"
import { TournamentTeamRow } from "@/features/teams/components/tournament-team-row"
import { useCategories, useGroups, useMyOrganization } from "@/features/tournaments/api"
import { useTournamentContext } from "@/features/tournaments/use-tournament-context"
import { plural } from "@/lib/format"

export function TournamentTeamsView() {
  const { id, tournament, isOrganizer } = useTournamentContext()
  const teams = useTournamentTeams(id)
  const categories = useCategories(id)
  const groups = useGroups(id)
  const organization = useMyOrganization()

  // Odrzucone i wycofane nie grają — nie zaśmiecają listy. Można je zgłosić ponownie ("Zgłoś drużynę").
  const active = teams.data?.filter(
    (t) => t.registrationStatus === "Pending" || t.registrationStatus === "Accepted",
  )
  const inactiveCount = (teams.data?.length ?? 0) - (active?.length ?? 0)
  const accepted = teams.data?.filter((t) => t.registrationStatus === "Accepted").length ?? 0
  const teamLimit = tournament.data?.teamLimit ?? 0

  const addButton = isOrganizer &&
    organization.data &&
    categories.data &&
    categories.data.length > 0 && (
      <AddTeamDialog
        tournamentId={id}
        organizationId={organization.data.id}
        categories={categories.data}
        registered={teams.data ?? []}
        trigger={
          <Button>
            <Plus />
            Zgłoś drużynę
          </Button>
        }
      />
    )

  if (teams.isPending || categories.isPending || groups.isPending) {
    return <Skeleton className="h-64 rounded-xl" />
  }

  if (categories.data?.length === 0) {
    return (
      <EmptyState
        icon={Layers}
        title="Najpierw dodaj kategorię"
        description="Drużyny zgłasza się do konkretnej kategorii wiekowej."
        action={
          isOrganizer && (
            <Button asChild variant="outline">
              <Link href={`/tournaments/${id}/categories`}>Przejdź do kategorii</Link>
            </Button>
          )
        }
      />
    )
  }

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-end justify-between gap-3">
        <div>
          <h2 className="text-lg font-semibold">Zgłoszone drużyny</h2>
          <p className="text-muted-foreground text-sm">
            Zaakceptowane: <span className="font-mono tabular-nums">{accepted}</span> /{" "}
            <span className="font-mono tabular-nums">{teamLimit}</span>
            {isOrganizer && inactiveCount > 0 && (
              <span className="block text-xs">
                {plural(inactiveCount, [
                  "odrzucona lub wycofana drużyna ukryta",
                  "odrzucone lub wycofane drużyny ukryte",
                  "odrzuconych lub wycofanych drużyn ukrytych",
                ])}{" "}
                — możesz zgłosić je ponownie.
              </span>
            )}
          </p>
        </div>
        <div className="flex flex-wrap gap-2">
          {isOrganizer && active && active.length > 0 && (
            <Button asChild variant="outline">
              <Link href="/players/new">
                <UserPlus />
                Nowy zawodnik
              </Link>
            </Button>
          )}
          {active && active.length > 0 && addButton}
        </div>
      </div>

      {!active?.length ? (
        <EmptyState
          icon={Shield}
          title="Brak zgłoszonych drużyn"
          description="Zgłoś drużyny z bazy organizacji — każda trafi jako oczekująca na akceptację."
          action={addButton || undefined}
        />
      ) : (
        categories.data!.map((category) => {
          const categoryTeams = active.filter((t) => t.categoryId === category.id)
          if (categoryTeams.length === 0) return null
          return (
            <Card key={category.id}>
              <CardHeader>
                <CardTitle className="text-base">{category.name}</CardTitle>
                <CardDescription>
                  {plural(categoryTeams.length, ["drużyna", "drużyny", "drużyn"])}
                </CardDescription>
              </CardHeader>
              <CardContent>
                <ul className="divide-y">
                  {categoryTeams.map((team) => (
                    <TournamentTeamRow
                      key={team.id}
                      tournamentId={id}
                      team={team}
                      // Puchar nie używa grup — bez wyboru grupy przy drużynach.
                      groups={
                        tournament.data?.format === "Knockout"
                          ? []
                          : (groups.data ?? []).filter((g) => g.categoryId === category.id)
                      }
                      canManage={isOrganizer}
                    />
                  ))}
                </ul>
              </CardContent>
            </Card>
          )
        })
      )}
    </div>
  )
}
