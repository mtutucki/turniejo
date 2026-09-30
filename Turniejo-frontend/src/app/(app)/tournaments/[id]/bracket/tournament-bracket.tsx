"use client"

import { GitBranch } from "lucide-react"
import Link from "next/link"
import { useState } from "react"

import { EmptyState } from "@/components/layout/empty-state"
import { Button } from "@/components/ui/button"
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card"
import { Skeleton } from "@/components/ui/skeleton"
import { useBracket, useSchedule } from "@/features/matches/api"
import { BracketBoard } from "@/features/matches/components/bracket-board"
import { GenerateBracketForm } from "@/features/matches/components/generate-bracket-form"
import { useTournamentTeams } from "@/features/teams/api"
import { useCategories, useGroups, useTournamentSettings } from "@/features/tournaments/api"
import { useTournamentContext } from "@/features/tournaments/use-tournament-context"
import { cn } from "@/lib/utils"

function CategoryBracket({ categoryId }: { categoryId: string }) {
  const { id, isOrganizer, tournament } = useTournamentContext()
  const tournamentFormat = tournament.data?.format
  const tournamentStatus = tournament.data?.status
  const bracket = useBracket(id, categoryId)
  const schedule = useSchedule(id)
  const groups = useGroups(id)
  const teams = useTournamentTeams(id)
  const settings = useTournamentSettings(id)

  if (bracket.isPending || groups.isPending || teams.isPending) {
    return <Skeleton className="h-64 w-full rounded-xl" />
  }

  // Puchar: pary tylko z drużyn — grupy mogą zostać po zmianie formatu, ale tu nie grają.
  const categoryGroups =
    tournamentFormat === "Knockout"
      ? []
      : (groups.data ?? []).filter((g) => g.categoryId === categoryId)
  const teamById = new Map((teams.data ?? []).map((t) => [t.id, t.teamName]))
  const groupById = new Map((groups.data ?? []).map((g) => [g.id, g.name]))

  if (bracket.data) {
    // Puchar: drabinka gotowa, ale mecze 1. rundy nie mają jeszcze godzin — następny krok to terminarz.
    const needsSchedule =
      isOrganizer && tournamentFormat === "Knockout" && tournamentStatus === "RegistrationClosed"
    return (
      <div className="space-y-4">
        {needsSchedule && (
          <div
            role="status"
            className="bg-primary/10 border-primary/40 flex flex-wrap items-center justify-between gap-3 rounded-xl border px-4 py-3 text-sm"
          >
            <p>Pary gotowe. Teraz wygeneruj terminarz — mecze 1. rundy dostaną godziny i boiska.</p>
            <Button asChild size="sm">
              <Link href={`/tournaments/${id}/schedule/generate`}>Generuj terminarz</Link>
            </Button>
          </div>
        )}
        <BracketBoard
          bracket={bracket.data}
          matches={schedule.data ?? []}
          lookups={{
            teamName: (teamId) => teamById.get(teamId) ?? "Drużyna",
            groupName: (groupId) => (groupId ? (groupById.get(groupId) ?? null) : null),
          }}
        />
      </div>
    )
  }

  if (!isOrganizer) {
    return (
      <EmptyState
        icon={GitBranch}
        title="Drabinka jeszcze nie powstała"
        description="Organizator utworzy ją po fazie grupowej."
      />
    )
  }

  return (
    <Card className="max-w-3xl">
      <CardHeader>
        <CardTitle className="text-base">Utwórz drabinkę</CardTitle>
        <CardDescription>
          Wybierz pary pierwszej rundy — z miejsc w grupach albo konkretne drużyny.
        </CardDescription>
      </CardHeader>
      <CardContent>
        <GenerateBracketForm
          tournamentId={id}
          categoryId={categoryId}
          groups={categoryGroups}
          teams={(teams.data ?? []).filter(
            (t) => t.categoryId === categoryId && t.registrationStatus === "Accepted",
          )}
          thirdPlaceEnabled={settings.data?.isThirdPlaceMatchEnabled ?? false}
        />
      </CardContent>
    </Card>
  )
}

export function TournamentBracketView() {
  const { id, tournament } = useTournamentContext()
  const categories = useCategories(id)
  const [selected, setSelected] = useState<string | undefined>()
  const format = tournament.data?.format

  if (format !== "Knockout" && format !== "GroupAndKnockout") {
    return (
      <EmptyState
        icon={GitBranch}
        title="Ten turniej nie ma fazy pucharowej"
        description="Drabinka jest dostępna w formatach „Pucharowy” i „Grupy + puchar” (Ustawienia → Format)."
      />
    )
  }
  if (categories.isPending) return <Skeleton className="h-64 w-full rounded-xl" />
  const list = categories.data ?? []
  const categoryId = selected ?? list[0]?.id
  if (!categoryId) return <EmptyState icon={GitBranch} title="Najpierw dodaj kategorię" />

  return (
    <div className="space-y-4">
      {list.length > 1 && (
        <div className="flex flex-wrap gap-2" role="group" aria-label="Kategoria">
          {list.map((c) => (
            <button
              key={c.id}
              type="button"
              aria-pressed={c.id === categoryId}
              onClick={() => setSelected(c.id)}
              className={cn(
                "h-9 rounded-full border px-4 text-sm font-medium",
                c.id === categoryId
                  ? "bg-primary text-primary-foreground border-primary"
                  : "bg-card text-muted-foreground",
              )}
            >
              {c.name}
            </button>
          ))}
        </div>
      )}
      <CategoryBracket key={categoryId} categoryId={categoryId} />
    </div>
  )
}
