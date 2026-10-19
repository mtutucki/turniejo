"use client"

import { useQueryClient } from "@tanstack/react-query"
import { Loader2, Plus, Trophy } from "lucide-react"
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
import { TournamentSections } from "@/features/tournaments/components/tournament-card"

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

      {isProvisioning ? (
        // Bez komunikatu same szkielety wyglądały po rejestracji jak zawieszona aplikacja.
        <div
          role="status"
          className="flex flex-col items-center justify-center gap-3 rounded-xl border border-dashed px-4 py-12 text-center"
        >
          <Loader2 className="text-primary size-8 animate-spin" />
          <div className="space-y-1">
            <p className="font-medium">Konfigurujemy Twoje konto</p>
            <p className="text-muted-foreground text-sm">
              Zakładamy Twoją organizację — to potrwa kilka sekund.
            </p>
          </div>
        </div>
      ) : isLoading ? (
        <Skeleton className="h-40 rounded-xl" />
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
        <TournamentSections items={tournaments.data.map((tournament) => ({ tournament }))} />
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
