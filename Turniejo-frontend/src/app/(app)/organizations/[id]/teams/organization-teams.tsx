"use client"

import { Pencil, Plus, Shield, Tags, Trash2, Users } from "lucide-react"
import Link from "next/link"
import { useParams, usePathname, useRouter, useSearchParams } from "next/navigation"
import { useState } from "react"
import { toast } from "sonner"

import { EmptyState } from "@/components/layout/empty-state"
import { PageHeader } from "@/components/layout/page-header"
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
  AlertDialogTrigger,
} from "@/components/ui/alert-dialog"
import { Badge } from "@/components/ui/badge"
import { Button } from "@/components/ui/button"
import { Card, CardContent } from "@/components/ui/card"
import { Input } from "@/components/ui/input"
import { Skeleton } from "@/components/ui/skeleton"
import { useDeleteTeamLabel, useOrganizationTeams, useTeamLabels } from "@/features/teams/api"
import {
  TeamLabelFormDialog,
  TeamLabelMembersDialog,
} from "@/features/teams/components/team-label-dialogs"
import { TeamLabelFilter, TeamLabelsHint } from "@/features/teams/components/team-label-filter"
import { TeamFormDialog } from "@/features/teams/components/team-form-dialog"
import { TeamLogo } from "@/features/teams/components/team-logo"
import { ALL_TEAMS, filterTeams, type LabelFilter, MANY_TEAMS } from "@/features/teams/team-labels"
import { useMyOrganization } from "@/features/tournaments/api"
import { isApiError } from "@/lib/api/errors"
import { plural } from "@/lib/format"
import { cn } from "@/lib/utils"
import type { Team, TeamLabel } from "@/types/teams"

type Tab = "teams" | "labels"

export function OrganizationTeams() {
  const { id } = useParams<{ id: string }>()
  const searchParams = useSearchParams()
  const router = useRouter()
  const pathname = usePathname()
  const tab: Tab = searchParams.get("tab") === "labels" ? "labels" : "teams"
  const organization = useMyOrganization()
  const teams = useOrganizationTeams(id)
  const labels = useTeamLabels(id)

  const addButton = (
    <TeamFormDialog
      organizationId={id}
      labels={labels.data ?? []}
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
        actions={tab === "teams" && teams.data && teams.data.length > 0 && addButton}
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
      ) : (
        <div className="space-y-4">
          <nav aria-label="Sekcje" className="-mx-4 overflow-x-auto px-4 lg:mx-0 lg:px-0">
            <ul className="flex min-w-max gap-1 border-b">
              {(
                [
                  ["teams", "Drużyny"],
                  ["labels", "Etykiety"],
                ] as const
              ).map(([key, label]) => (
                <li key={key}>
                  <button
                    type="button"
                    aria-current={tab === key ? "page" : undefined}
                    onClick={() =>
                      router.replace(key === "labels" ? `${pathname}?tab=labels` : pathname, {
                        scroll: false,
                      })
                    }
                    className={cn(
                      "-mb-px block border-b-2 px-3 py-2 text-sm font-medium transition-colors",
                      tab === key
                        ? "border-primary text-primary"
                        : "text-muted-foreground hover:text-foreground border-transparent",
                    )}
                  >
                    {label}
                  </button>
                </li>
              ))}
            </ul>
          </nav>

          {tab === "labels" ? (
            <LabelsTab organizationId={id} teams={teams.data} labels={labels.data} />
          ) : teams.data.length === 0 ? (
            <EmptyState
              icon={Shield}
              title="Brak drużyn"
              description="Dodaj drużyny, które będą grać w Twoich turniejach."
              action={addButton}
            />
          ) : (
            <TeamsTab organizationId={id} teams={teams.data} labels={labels.data ?? []} />
          )}
        </div>
      )}
    </>
  )
}

function TeamsTab({
  organizationId,
  teams,
  labels,
}: {
  organizationId: string
  teams: Team[]
  labels: TeamLabel[]
}) {
  const [filter, setFilter] = useState<LabelFilter>(ALL_TEAMS)
  const [query, setQuery] = useState("")
  // Usunięta etykieta nie może zostać wybranym filtrem z pustą listą.
  const activeFilter =
    filter.kind === "label" && !labels.some((l) => l.id === filter.labelId) ? ALL_TEAMS : filter
  const visible = filterTeams(teams, activeFilter, query)
  const labelName = new Map(labels.map((l) => [l.id, l.name]))

  return (
    <div className="space-y-3">
      <TeamLabelsHint
        organizationId={organizationId}
        teamCount={teams.length}
        labelCount={labels.length}
      />
      {(teams.length > MANY_TEAMS || labels.length > 0) && (
        <Input
          type="search"
          placeholder="Szukaj drużyny…"
          aria-label="Szukaj drużyny"
          value={query}
          onChange={(e) => setQuery(e.target.value)}
          className="sm:max-w-xs"
        />
      )}
      <TeamLabelFilter labels={labels} teams={teams} value={activeFilter} onChange={setFilter} />

      {visible.length === 0 ? (
        <p className="text-muted-foreground py-6 text-center text-sm">
          {query.trim()
            ? `Brak drużyn pasujących do „${query.trim()}”.`
            : "Brak drużyn w tym widoku."}
        </p>
      ) : (
        <ul className="grid gap-3 md:grid-cols-2 xl:grid-cols-3">
          {visible.map((team) => (
            // min-w-0: element siatki nie kurczy się poniżej treści, więc długa nazwa wypychała
            // przyciski poza ekran telefonu zamiast się skracać (QA v3: DRU-023).
            <li key={team.id} className="min-w-0">
              <Card className="min-w-0 py-4">
                <CardContent className="flex items-center gap-3 px-4">
                  <TeamLogo name={team.name} logoUrl={team.logoUrl} size={44} />
                  <div className="min-w-0 flex-1">
                    <Link
                      href={`/organizations/${organizationId}/teams/${team.id}`}
                      className="block hover:underline"
                    >
                      <p className="truncate font-medium">{team.name}</p>
                      {team.shortName && (
                        <p className="text-muted-foreground font-mono text-xs">{team.shortName}</p>
                      )}
                    </Link>
                    {team.labelIds.length > 0 && (
                      <ul aria-label="Etykiety drużyny" className="mt-1 flex flex-wrap gap-1">
                        {team.labelIds
                          .filter((labelId) => labelName.has(labelId))
                          .map((labelId) => (
                            <li key={labelId} className="max-w-full">
                              <Badge variant="secondary" className="max-w-full">
                                <span className="truncate">{labelName.get(labelId)}</span>
                              </Badge>
                            </li>
                          ))}
                      </ul>
                    )}
                  </div>
                  <Button asChild variant="ghost" size="sm">
                    <Link href={`/organizations/${organizationId}/teams/${team.id}`}>
                      <Users />
                      Kadra
                    </Link>
                  </Button>
                  <TeamFormDialog
                    organizationId={organizationId}
                    team={team}
                    labels={labels}
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
    </div>
  )
}

function LabelsTab({
  organizationId,
  teams,
  labels,
}: {
  organizationId: string
  teams: Team[]
  labels: TeamLabel[] | undefined
}) {
  const deleteLabel = useDeleteTeamLabel(organizationId)

  const addButton = (
    <TeamLabelFormDialog
      organizationId={organizationId}
      trigger={
        <Button>
          <Plus />
          Nowa etykieta
        </Button>
      }
    />
  )

  if (!labels) return <Skeleton className="h-32 rounded-xl" />
  if (labels.length === 0) {
    return (
      <EmptyState
        icon={Tags}
        title="Brak etykiet"
        description="Etykiety grupują drużyny, np. według rocznika albo klubu. Drużyna może mieć kilka."
        action={addButton}
      />
    )
  }

  return (
    <div className="space-y-3">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <p className="text-muted-foreground text-sm">
          Drużyna może mieć kilka etykiet. Filtrujesz po nich listę drużyn i wybór drużyn do
          turnieju.
        </p>
        {addButton}
      </div>
      <ul className="divide-y rounded-xl border">
        {labels.map((label) => (
          <li key={label.id} className="flex flex-wrap items-center gap-2 px-4 py-3">
            <div className="min-w-0 flex-1">
              <p className="truncate font-medium">{label.name}</p>
              <p className="text-muted-foreground text-sm">
                {label.teamCount === 0
                  ? "Bez drużyn"
                  : plural(label.teamCount, ["drużyna", "drużyny", "drużyn"])}
              </p>
            </div>
            <div className="flex items-center gap-1">
              <TeamLabelMembersDialog
                organizationId={organizationId}
                label={label}
                teams={teams}
                trigger={
                  <Button variant="outline" size="sm" aria-label={`Wybierz drużyny: ${label.name}`}>
                    <Users />
                    Drużyny
                  </Button>
                }
              />
              <TeamLabelFormDialog
                organizationId={organizationId}
                label={label}
                trigger={
                  <Button
                    variant="ghost"
                    size="icon"
                    aria-label={`Zmień nazwę etykiety ${label.name}`}
                  >
                    <Pencil />
                  </Button>
                }
              />
              <AlertDialog>
                <AlertDialogTrigger asChild>
                  <Button
                    variant="ghost"
                    size="icon"
                    aria-label={`Usuń etykietę ${label.name}`}
                    disabled={deleteLabel.isPending}
                  >
                    <Trash2 />
                  </Button>
                </AlertDialogTrigger>
                <AlertDialogContent>
                  <AlertDialogHeader>
                    <AlertDialogTitle>Usunąć etykietę {label.name}?</AlertDialogTitle>
                    <AlertDialogDescription>
                      Drużyny zostaną w bazie, zniknie tylko etykieta.
                    </AlertDialogDescription>
                  </AlertDialogHeader>
                  <AlertDialogFooter>
                    <AlertDialogCancel>Anuluj</AlertDialogCancel>
                    <AlertDialogAction
                      onClick={() =>
                        deleteLabel.mutate(label.id, {
                          onSuccess: () => toast.success("Etykieta usunięta"),
                        })
                      }
                    >
                      Usuń
                    </AlertDialogAction>
                  </AlertDialogFooter>
                </AlertDialogContent>
              </AlertDialog>
            </div>
          </li>
        ))}
      </ul>
    </div>
  )
}
