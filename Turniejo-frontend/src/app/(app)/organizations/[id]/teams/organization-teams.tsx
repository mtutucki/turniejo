"use client"

import { Pencil, Plus, Shield, Users } from "lucide-react"
import Link from "next/link"
import { useParams } from "next/navigation"

import { EmptyState } from "@/components/layout/empty-state"
import { PageHeader } from "@/components/layout/page-header"
import { Button } from "@/components/ui/button"
import { Card, CardContent } from "@/components/ui/card"
import { Skeleton } from "@/components/ui/skeleton"
import { useOrganizationTeams } from "@/features/teams/api"
import { TeamFormDialog } from "@/features/teams/components/team-form-dialog"
import { TeamLogo } from "@/features/teams/components/team-logo"
import { useMyOrganization } from "@/features/tournaments/api"
import { isApiError } from "@/lib/api/errors"

export function OrganizationTeams() {
  const { id } = useParams<{ id: string }>()
  const organization = useMyOrganization()
  const teams = useOrganizationTeams(id)

  const addButton = (
    <TeamFormDialog
      organizationId={id}
      trigger={
        <Button>
          <Plus />
          Nowa drużyna
        </Button>
      }
    />
  )

  return (
    <>
      <PageHeader
        title="Baza drużyn"
        description={
          organization.data?.id === id
            ? `Organizacja: ${organization.data.name}`
            : "Drużyny organizacji"
        }
        actions={teams.data && teams.data.length > 0 && addButton}
      />

      {teams.isPending ? (
        <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-3">
          {[0, 1, 2].map((i) => (
            <Skeleton key={i} className="h-20 rounded-xl" />
          ))}
        </div>
      ) : teams.isError ? (
        <EmptyState
          icon={Shield}
          title={
            isApiError(teams.error) && teams.error.status === 403
              ? "Tylko organizator widzi bazę drużyn"
              : "Nie udało się pobrać drużyn"
          }
        />
      ) : teams.data.length === 0 ? (
        <EmptyState
          icon={Shield}
          title="Brak drużyn"
          description="Dodaj drużyny, które będą grać w Twoich turniejach."
          action={addButton}
        />
      ) : (
        <ul className="grid gap-3 md:grid-cols-2 xl:grid-cols-3">
          {teams.data.map((team) => (
            <li key={team.id}>
              <Card className="py-4">
                <CardContent className="flex items-center gap-3 px-4">
                  <TeamLogo name={team.name} logoUrl={team.logoUrl} size={44} />
                  <Link
                    href={`/organizations/${id}/teams/${team.id}`}
                    className="min-w-0 flex-1 hover:underline"
                  >
                    <p className="truncate font-medium">{team.name}</p>
                    {team.shortName && (
                      <p className="text-muted-foreground font-mono text-xs">{team.shortName}</p>
                    )}
                  </Link>
                  <Button asChild variant="ghost" size="sm">
                    <Link href={`/organizations/${id}/teams/${team.id}`}>
                      <Users />
                      Kadra
                    </Link>
                  </Button>
                  <TeamFormDialog
                    organizationId={id}
                    team={team}
                    trigger={
                      <Button
                        variant="ghost"
                        size="icon"
                        aria-label={`Edytuj drużynę ${team.name}`}
                      >
                        <Pencil />
                      </Button>
                    }
                  />
                </CardContent>
              </Card>
            </li>
          ))}
        </ul>
      )}
    </>
  )
}
