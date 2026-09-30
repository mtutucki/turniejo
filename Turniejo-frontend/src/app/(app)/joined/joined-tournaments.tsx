"use client"

import { useQueries } from "@tanstack/react-query"
import { Medal, Ticket } from "lucide-react"
import Link from "next/link"

import { EmptyState } from "@/components/layout/empty-state"
import { PageHeader } from "@/components/layout/page-header"
import { Button } from "@/components/ui/button"
import { Skeleton } from "@/components/ui/skeleton"
import { tournamentKeys } from "@/features/tournaments/api"
import { TournamentCard } from "@/features/tournaments/components/tournament-card"
import { useApi } from "@/hooks/use-api"
import { joinedTournaments } from "@/lib/auth/role-landing"
import { useAuthStore } from "@/stores/auth-store"
import { JOIN_ROLE_LABELS } from "@/types/teams"
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

  return (
    <>
      <PageHeader
        title="Dołączone turnieje"
        description="Turnieje, do których dołączyłeś kodem od organizatora."
        actions={joined.length > 0 && <JoinButton />}
      />

      {!roles ? (
        <div className="grid gap-4 md:grid-cols-2">
          {[0, 1].map((i) => (
            <Skeleton key={i} className="h-40 rounded-xl" />
          ))}
        </div>
      ) : joined.length === 0 ? (
        <EmptyState
          icon={Medal}
          title="Nie dołączyłeś jeszcze do żadnego turnieju"
          description="Kod (albo link z kodem) dostaniesz od organizatora turnieju."
          action={<JoinButton />}
        />
      ) : (
        <div className="grid gap-4 md:grid-cols-2">
          {joined.map(({ id, roles: tournamentRoles }, index) => {
            const tournament = tournaments[index]
            if (tournament?.data) {
              return (
                <TournamentCard
                  key={id}
                  tournament={tournament.data}
                  roleLabel={tournamentRoles
                    .map((role) => JOIN_ROLE_LABELS[role] ?? role)
                    .join(", ")}
                />
              )
            }
            // Usunięty turniej albo brak dostępu — nie blokujemy reszty listy.
            if (tournament?.isError) return null
            return <Skeleton key={id} className="h-40 rounded-xl" />
          })}
        </div>
      )}
    </>
  )
}
