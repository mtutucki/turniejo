"use client"

import { CircleAlert, Layers, Plus, Shield, UserPlus } from "lucide-react"
import Link from "next/link"

import { EmptyState } from "@/components/layout/empty-state"
import { Button } from "@/components/ui/button"
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card"
import { Skeleton } from "@/components/ui/skeleton"
import { useTournamentTeams } from "@/features/teams/api"
import { AcceptPendingTeams } from "@/features/teams/components/accept-pending-teams"
import { AddTeamDialog } from "@/features/teams/components/add-team-dialog"
import { TournamentTeamRow } from "@/features/teams/components/tournament-team-row"
import { useCategories, useGroups, useMyOrganization } from "@/features/tournaments/api"
import { useTournamentContext } from "@/features/tournaments/use-tournament-context"
import { groupSections } from "@/features/teams/group-sections"
import { plural } from "@/lib/format"
import { cn } from "@/lib/utils"
import type { TournamentTeam } from "@/types/teams"
import { hasKnockoutStage, type TournamentGroup } from "@/types/tournaments"

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
  // Backend liczy limit po zaakceptowanych: przy komplecie odrzuca akceptację i nowe zgłoszenia.
  const freeSlots = Math.max(teamLimit - accepted, 0)
  const limitReached = !!tournament.data && freeSlots === 0
  // Mecze powstały z tej listy i grup: backend nie pozwoli zmienić grupy ani przyjąć drużyny.
  const status = tournament.data?.status
  const scheduleLocked =
    status === "ScheduleGenerated" || status === "InProgress" || status === "Finished"
  // Wycofanie przy gotowym terminarzu odwołuje mecze drużyny (w trwającym pucharze: walkower dla
  // rywala, inaczej drabinka by stanęła), więc organizator potwierdza je z tą informacją.
  const withdrawWarning = !scheduleLocked
    ? undefined
    : status === "InProgress" && hasKnockoutStage(tournament.data?.format)
      ? "Jej nierozegrane mecze zostaną odwołane, a w pucharze rywal dostanie walkower."
      : "Jej nierozegrane mecze zostaną odwołane."
  // W kolejności kategorii, tak jak na liście: akceptacja przy małym limicie bierze te z góry.
  const pendingTeams = (categories.data ?? []).flatMap((category) =>
    (active ?? []).filter(
      (t) => t.categoryId === category.id && t.registrationStatus === "Pending",
    ),
  )

  const addButton = isOrganizer &&
    organization.data &&
    categories.data &&
    categories.data.length > 0 && (
      <AddTeamDialog
        tournamentId={id}
        organizationId={organization.data.id}
        categories={categories.data}
        registered={teams.data ?? []}
        // Oczekujące też zajmują miejsce: każde zgłoszenie ma szansę na akceptację.
        freeSlots={Math.max(teamLimit - (active?.length ?? 0), 0)}
        teamLimit={teamLimit}
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
                - możesz zgłosić je ponownie.
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

      {isOrganizer && limitReached && (
        <div className="border-live/40 bg-live/5 flex flex-wrap items-center justify-between gap-3 rounded-xl border px-4 py-3 text-sm">
          <p className="flex items-start gap-2">
            <CircleAlert className="text-live mt-0.5 size-4 shrink-0" aria-hidden />
            <span>
              Limit drużyn jest wyczerpany ({accepted} z {teamLimit}). Żeby przyjąć kolejną, zwiększ
              limit albo wycofaj którąś z zaakceptowanych.
            </span>
          </p>
          <Button asChild size="sm" variant="outline">
            <Link href={`/tournaments/${id}/settings#details`}>Zmień limit</Link>
          </Button>
        </div>
      )}

      {/* Przed startem terminarz da się jeszcze wyczyścić, potem zmiany drużyn i grup są zamknięte. */}
      {isOrganizer && status === "ScheduleGenerated" && (
        <div className="bg-muted/50 flex flex-wrap items-center justify-between gap-3 rounded-xl border px-4 py-3 text-sm">
          <p>
            Terminarz jest gotowy, więc grup i listy drużyn nie zmienisz. Żeby to zrobić, wyczyść
            terminarz.
          </p>
          <Button asChild size="sm" variant="outline">
            <Link href={`/tournaments/${id}/schedule`}>Terminarz</Link>
          </Button>
        </div>
      )}

      {/* Przy wyczerpanym limicie zostaje sam baner o limicie powyżej. */}
      {isOrganizer && !scheduleLocked && pendingTeams.length > 0 && freeSlots > 0 && (
        <AcceptPendingTeams tournamentId={id} teams={pendingTeams} freeSlots={freeSlots} />
      )}

      {!active?.length ? (
        <EmptyState
          icon={Shield}
          title="Brak zgłoszonych drużyn"
          description="Zgłoś drużyny z bazy organizacji - każda trafi jako oczekująca na akceptację."
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
                <CategoryTeamList
                  tournamentId={id}
                  teams={categoryTeams}
                  // Puchar nie używa grup — bez wyboru grupy przy drużynach. Do grup drugiej fazy
                  // drużyny trafiają same, z tabel pierwszej fazy.
                  groups={
                    tournament.data?.format === "Knockout"
                      ? []
                      : (groups.data ?? []).filter(
                          (g) => g.categoryId === category.id && g.stage !== 2,
                        )
                  }
                  canManage={isOrganizer}
                  limitReached={limitReached}
                  scheduleLocked={scheduleLocked}
                  withdrawWarning={withdrawWarning}
                />
              </CardContent>
            </Card>
          )
        })
      )}
    </div>
  )
}

/**
 * Drużyny jednej kategorii. Przy kilku grupach każda grupa ma własną ramkę z nagłówkiem, żeby skład
 * grup było widać bez czytania selectów. Drużyny bez grupy są na górze, wyróżnione: nie zagrają.
 */
function CategoryTeamList({
  tournamentId,
  teams,
  groups,
  canManage,
  limitReached,
  scheduleLocked,
  withdrawWarning,
}: {
  tournamentId: string
  teams: TournamentTeam[]
  groups: TournamentGroup[]
  canManage: boolean
  limitReached: boolean
  scheduleLocked: boolean
  withdrawWarning?: string
}) {
  const sections = groupSections(teams, groups)
  const row = (team: TournamentTeam) => (
    <TournamentTeamRow
      key={team.id}
      tournamentId={tournamentId}
      team={team}
      groups={groups}
      canManage={canManage}
      limitReached={limitReached}
      scheduleLocked={scheduleLocked}
      withdrawWarning={withdrawWarning}
      inGroupSection={!!sections}
    />
  )

  if (!sections) return <ul className="divide-y">{teams.map(row)}</ul>

  return (
    <div className="space-y-4">
      {sections.map(({ group, teams: sectionTeams }) => {
        const title = group?.name ?? "Bez grupy"
        return (
          <section
            key={group?.id ?? "none"}
            aria-label={title}
            className={cn(
              "overflow-hidden rounded-lg border",
              group ? "bg-muted/20" : "border-live/40 bg-live/5",
            )}
          >
            <header
              className={cn(
                "flex flex-wrap items-center gap-x-3 gap-y-1 border-b px-3 py-2",
                group ? "bg-muted/50" : "border-live/40",
              )}
            >
              {group ? (
                <span
                  className="bg-primary/15 text-primary flex size-6 shrink-0 items-center justify-center rounded-md text-xs font-semibold"
                  aria-hidden
                >
                  {group.name.replace(/^grupa\s+/i, "").slice(0, 2).toUpperCase()}
                </span>
              ) : (
                <CircleAlert className="text-live size-5 shrink-0" aria-hidden />
              )}
              <h3 className="text-sm font-semibold">{title}</h3>
              <span className="text-muted-foreground font-mono text-xs tabular-nums">
                {sectionTeams.length}
              </span>
              {!group && (
                <span className="text-muted-foreground basis-full text-xs">
                  Te drużyny nie zagrają, dopóki nie trafią do grupy.
                </span>
              )}
            </header>
            {sectionTeams.length > 0 ? (
              <ul className="divide-y px-3">{sectionTeams.map(row)}</ul>
            ) : (
              <p className="text-muted-foreground px-3 py-3 text-sm">Brak drużyn w tej grupie.</p>
            )}
          </section>
        )
      })}
    </div>
  )
}
