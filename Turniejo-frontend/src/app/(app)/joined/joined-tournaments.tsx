"use client"

import { useQueries } from "@tanstack/react-query"
import { Medal, Ticket } from "lucide-react"
import Link from "next/link"

import { EmptyState } from "@/components/layout/empty-state"
import { PageHeader } from "@/components/layout/page-header"
import { Button } from "@/components/ui/button"
import { Skeleton } from "@/components/ui/skeleton"
import { tournamentKeys } from "@/features/tournaments/api"
import {
  type TournamentListItem,
  TournamentSections,
} from "@/features/tournaments/components/tournament-card"
import { useApi } from "@/hooks/use-api"
import { joinedTournaments } from "@/lib/auth/role-landing"
import { useAuthStore } from "@/stores/auth-store"
import type { UserRole } from "@/types/roles"
import type { Tournament } from "@/types/tournaments"

function JoinButton() {
  return (
    <Button asChild>
      <Link href="/join">
        <Ticket />
        Dołącz kodem
      </Link>
    </Button>
  )
}

/** Turnieje, do których user dołączył kodem od organizatora (sędzia, trener, kibic/rodzic). */
export function JoinedTournaments() {
  const roles = useAuthStore((state) => state.roleAssignments)
  const joined = roles ? joinedTournaments(roles) : []
  const { request, isAuthenticated } = useApi()
  const tournaments = useQueries({
    queries: joined.map(({ id }) => ({
      queryKey: tournamentKeys.detail(id),
      queryFn: () => request<Tournament>(`/api/tournaments/${id}`),
      enabled: isAuthenticated,
    })),
  })
  // Usunięty turniej albo brak dostępu kończy się błędem — pomijamy go, nie blokując reszty listy.
  const items: TournamentListItem[] = joined.flatMap(({ roles: tournamentRoles }, index) => {
    const tournament = tournaments[index]?.data
    return tournament ? [{ tournament, roles: tournamentRoles as UserRole[] }] : []
  })
  const stillLoading = tournaments.some((query) => query.isPending)

  return (
    <>
      <PageHeader
        title="Dołączone turnieje"
        description="Turnieje, do których dołączyłeś kodem od organizatora."
        actions={joined.length > 0 && <JoinButton />}
      />

      {!roles ? (
        <Skeleton className="h-40 rounded-xl" />
      ) : joined.length === 0 || (items.length === 0 && !stillLoading) ? (
        <EmptyState
          icon={Medal}
          title="Nie dołączyłeś jeszcze do żadnego turnieju"
          description="Kod (albo link z kodem) dostaniesz od organizatora turnieju."
          action={<JoinButton />}
        />
      ) : (
        <div className="space-y-6">
          <TournamentSections items={items} />
          {stillLoading && <Skeleton className="h-16 rounded-xl" />}
        </div>
      )}
    </>
  )
}
