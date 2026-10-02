"use client"

import { useQueryClient } from "@tanstack/react-query"
import { Plus, Trophy } from "lucide-react"
import Link from "next/link"
import { useEffect } from "react"

import { EmptyState } from "@/components/layout/empty-state"
import { PageHeader } from "@/components/layout/page-header"
import { Button } from "@/components/ui/button"
import { Skeleton } from "@/components/ui/skeleton"
import {
  clearOrganizationProvisioning,
  isOrganizationProvisioning,
  tournamentKeys,
  useMyOrganization,
  useTournaments,
} from "@/features/tournaments/api"
import { BecomeOrganizerDialog } from "@/features/tournaments/components/become-organizer-dialog"
import { TournamentCard } from "@/features/tournaments/components/tournament-card"

function NewTournamentButton() {
  return (
    <Button asChild>
      <Link href="/tournaments/new">
        <Plus />
        Nowy turniej
      </Link>
    </Button>
  )
}

export function TournamentList() {
  const organization = useMyOrganization()
  const tournaments = useTournaments(organization.data?.id)
  // isPending (nie isLoading): zapytanie czekające na sesję jest wyłączone, a wtedy isLoading = false.
  // Tuż po rejestracji organizatora organizacja jeszcze powstaje — czekamy, zamiast proponować jej założenie.
  // Licznik odpytań żyje w cache zapytania; odczyt isFetching gwarantuje re-render po każdym z nich.
  const pollCount =
    useQueryClient().getQueryState(tournamentKeys.myOrganization)?.dataUpdateCount ?? 0
  void organization.isFetching
  const isProvisioning = organization.data === null && isOrganizationProvisioning(pollCount)
  const isLoading =
    organization.isPending || isProvisioning || (!!organization.data && tournaments.isPending)

  const provisioningSettled = organization.isSuccess && !isProvisioning
  useEffect(() => {
    if (provisioningSettled) clearOrganizationProvisioning()
  }, [provisioningSettled])

  return (
    <>
      <PageHeader
        title="Moje turnieje"
        description={organization.data ? `Organizacja: ${organization.data.name}` : undefined}
        actions={organization.data && <NewTournamentButton />}
      />

      {isLoading ? (
        <div className="grid gap-4 md:grid-cols-2">
          {[0, 1].map((i) => (
            <Skeleton key={i} className="h-40 rounded-xl" />
          ))}
        </div>
      ) : organization.data === null ? (
        // Konto założone jako rodzic/trener/sędzia — organizację zakłada się na żądanie.
        <EmptyState
          icon={Trophy}
          title="Nie masz jeszcze organizacji"
          description="Turnieje tworzy się w ramach organizacji. Jeśli dostałeś kod od organizatora, dołącz nim do turnieju."
          action={
            <div className="flex flex-wrap justify-center gap-2">
              <BecomeOrganizerDialog trigger={<Button>Zostań organizatorem</Button>} />
              <Button asChild variant="outline">
                <Link href="/join">Dołącz kodem</Link>
              </Button>
            </div>
          }
        />
      ) : tournaments.isError || organization.isError ? (
        <EmptyState
          icon={Trophy}
          title="Nie udało się pobrać turniejów"
          description="Odśwież stronę i spróbuj ponownie."
        />
      ) : tournaments.data?.length ? (
        <div className="grid gap-4 md:grid-cols-2">
          {tournaments.data.map((tournament) => (
            <TournamentCard key={tournament.id} tournament={tournament} />
          ))}
        </div>
      ) : (
        <EmptyState
          icon={Trophy}
          title="Brak turniejów"
          description="Utwórz pierwszy turniej — potem dodasz kategorie, grupy i drużyny."
          action={<NewTournamentButton />}
        />
      )}
    </>
  )
}
