"use client"

import { useRouter } from "next/navigation"
import { toast } from "sonner"

import { PageHeader } from "@/components/layout/page-header"
import { Card, CardContent } from "@/components/ui/card"
import { Skeleton } from "@/components/ui/skeleton"
import { useCreateTournament, useMyOrganization } from "@/features/tournaments/api"
import { TournamentDetailsForm } from "@/features/tournaments/components/tournament-details-form"
import { useActiveTournamentStore } from "@/stores/active-tournament-store"
import type { TournamentDetailsInput } from "@/types/tournaments"

export function NewTournament() {
  const router = useRouter()
  const organization = useMyOrganization()
  const createTournament = useCreateTournament()
  const setActiveTournament = useActiveTournamentStore((state) => state.setTournament)

  async function create(input: TournamentDetailsInput) {
    const { tournamentId } = await createTournament.mutateAsync({
      ...input,
      organizationId: organization.data!.id,
    })
    setActiveTournament({ id: tournamentId, name: input.name })
    toast.success("Turniej utworzony")
    router.push(`/tournaments/${tournamentId}/dashboard`)
  }

  return (
    <>
      <PageHeader title="Nowy turniej" description="Podstawowe dane — resztę ustawisz później." />
      <Card className="max-w-2xl">
        <CardContent>
          {organization.isPending ? (
            <Skeleton className="h-64" />
          ) : organization.data ? (
            <TournamentDetailsForm
              submitLabel="Utwórz turniej"
              pendingLabel="Tworzenie…"
              onSubmit={create}
            />
          ) : (
            <p className="text-muted-foreground text-sm">
              Nie masz organizacji, w której można utworzyć turniej.
            </p>
          )}
        </CardContent>
      </Card>
    </>
  )
}
