"use client"

import { Trophy } from "lucide-react"
import Link from "next/link"
import { useRouter } from "next/navigation"
import { useState } from "react"
import { toast } from "sonner"

import { EmptyState } from "@/components/layout/empty-state"
import { PageHeader } from "@/components/layout/page-header"
import { Button } from "@/components/ui/button"
import { Card, CardContent } from "@/components/ui/card"
import { Field, FieldGroup, FieldLabel } from "@/components/ui/field"
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select"
import { Skeleton } from "@/components/ui/skeleton"
import { useAddNewPlayerToSquad, useTeamPlayers, useTournamentTeams } from "@/features/teams/api"
import { PlayerForm } from "@/features/teams/components/player-form"
import { useCategories } from "@/features/tournaments/api"
import { useActiveTournamentStore } from "@/stores/active-tournament-store"

function NewPlayerForTeam({
  tournamentId,
  tournamentTeamId,
}: {
  tournamentId: string
  tournamentTeamId: string
}) {
  const router = useRouter()
  const teams = useTournamentTeams(tournamentId)
  const categories = useCategories(tournamentId)
  const squad = useTeamPlayers(tournamentId, tournamentTeamId)
  const addPlayer = useAddNewPlayerToSquad(tournamentId, tournamentTeamId)
  const team = teams.data?.find((t) => t.id === tournamentTeamId)
  const category = categories.data?.find((c) => c.id === team?.categoryId)

  if (squad.isPending || categories.isPending) return <Skeleton className="h-72" />

  return (
    <PlayerForm
      // Nowa drużyna = inne reguły (kategoria, numery) — formularz od nowa.
      key={tournamentTeamId}
      category={category}
      squad={squad.data ?? []}
      submitLabel="Utwórz i dodaj do składu"
      onSubmit={async (input) => {
        await addPlayer.mutateAsync(input)
        toast.success(
          `${input.firstName} ${input.lastName} dodany do drużyny ${team?.teamName ?? ""}`,
        )
        router.push(`/tournaments/${tournamentId}/teams/${tournamentTeamId}`)
      }}
    />
  )
}

/**
 * Profil zawodnika zawsze trafia od razu do składu — backend nie ma listy zawodników, z której
 * dałoby się go później wybrać, więc samodzielny profil byłby nieosiągalny z UI.
 */
export function NewPlayer() {
  const tournament = useActiveTournamentStore((state) => state.tournament)
  const teams = useTournamentTeams(tournament?.id ?? "")
  const [tournamentTeamId, setTournamentTeamId] = useState<string>()
  const activeTeams = (teams.data ?? []).filter(
    (t) => t.registrationStatus === "Pending" || t.registrationStatus === "Accepted",
  )

  return (
    <>
      <PageHeader title="Nowy zawodnik" description="Profil zawodnika i przypisanie do drużyny." />
      {!tournament ? (
        <EmptyState
          icon={Trophy}
          title="Wybierz turniej"
          description="Zawodnik jest dodawany do drużyny w konkretnym turnieju."
          action={
            <Button asChild variant="outline">
              <Link href="/tournaments">Moje turnieje</Link>
            </Button>
          }
        />
      ) : (
        <Card className="max-w-2xl">
          <CardContent className="space-y-6">
            <FieldGroup>
              <Field>
                <FieldLabel htmlFor="new-player-team">
                  Drużyna w turnieju „{tournament.name}”
                </FieldLabel>
                <Select
                  value={tournamentTeamId}
                  onValueChange={setTournamentTeamId}
                  disabled={teams.isPending}
                >
                  <SelectTrigger id="new-player-team" className="w-full">
                    <SelectValue placeholder="Wybierz drużynę" />
                  </SelectTrigger>
                  <SelectContent>
                    {activeTeams.map((t) => (
                      <SelectItem key={t.id} value={t.id}>
                        {t.teamName}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
                {teams.data && activeTeams.length === 0 && (
                  <p className="text-muted-foreground text-sm">
                    W tym turnieju nie ma jeszcze drużyn.{" "}
                    <Link
                      href={`/tournaments/${tournament.id}/teams`}
                      className="text-primary hover:underline"
                    >
                      Zgłoś drużynę
                    </Link>
                  </p>
                )}
              </Field>
            </FieldGroup>
            {tournamentTeamId && (
              <NewPlayerForTeam tournamentId={tournament.id} tournamentTeamId={tournamentTeamId} />
            )}
          </CardContent>
        </Card>
      )}
    </>
  )
}
