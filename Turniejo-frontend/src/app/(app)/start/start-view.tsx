"use client"

import { useQueries } from "@tanstack/react-query"
import { Hourglass, Ticket, Trophy } from "lucide-react"
import { useSession } from "next-auth/react"
import Link from "next/link"
import { useRouter } from "next/navigation"
import { useEffect } from "react"

import { Button } from "@/components/ui/button"
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card"
import { Skeleton } from "@/components/ui/skeleton"
import { tournamentKeys } from "@/features/tournaments/api"
import { BecomeOrganizerDialog } from "@/features/tournaments/components/become-organizer-dialog"
import { useApi } from "@/hooks/use-api"
import { landingPath } from "@/lib/auth/role-landing"
import { useAuthStore } from "@/stores/auth-store"
import type { Tournament } from "@/types/tournaments"

/** Turnieje, do których user dołączył kodem, ale nikt go jeszcze nie przypisał (kibic/trener). */
function PendingMemberships({ tournamentIds }: { tournamentIds: { id: string; role: string }[] }) {
  const { request, isAuthenticated } = useApi()
  const tournaments = useQueries({
    queries: tournamentIds.map(({ id }) => ({
      queryKey: tournamentKeys.detail(id),
      queryFn: () => request<Tournament>(`/api/tournaments/${id}`),
      enabled: isAuthenticated,
    })),
  })

  return (
    <Card>
      <CardHeader>
        <CardTitle className="flex items-center gap-2 text-base">
          <Hourglass className="text-primary size-4" />
          Czekasz na organizatora
        </CardTitle>
        <CardDescription>
          Rodzica organizator przypisuje do dziecka, a trenera do drużyny — wtedy w menu pojawi się
          Twój panel. Do tego czasu możesz śledzić turniej jak kibic.
        </CardDescription>
      </CardHeader>
      <CardContent>
        <ul className="divide-y">
          {tournamentIds.map(({ id, role }, index) => {
            const tournament = tournaments[index]?.data
            return (
              <li key={id} className="flex items-center justify-between gap-3 py-2">
                <span className="min-w-0">
                  <span className="block truncate font-medium">
                    {tournament?.name ?? (
                      <span className="bg-muted block h-4 w-40 animate-pulse rounded" />
                    )}
                  </span>
                  <span className="text-muted-foreground text-xs">
                    {role === "Coach"
                      ? "Trener — czeka na przypisanie do drużyny"
                      : "Kibic / rodzic"}
                  </span>
                </span>
                {tournament &&
                  (tournament.isPublic ? (
                    <Button asChild size="sm" variant="outline">
                      <Link href={`/t/${tournament.slug}`}>Portal turnieju</Link>
                    </Button>
                  ) : (
                    <span className="text-muted-foreground shrink-0 text-xs">
                      Portal niedostępny
                    </span>
                  ))}
              </li>
            )
          })}
        </ul>
      </CardContent>
    </Card>
  )
}

/**
 * Strona startowa po zalogowaniu: kieruje do panelu najważniejszej roli (organizator → turnieje,
 * sędzia → mecze, trener → drużyny, rodzic → dzieci). Konto bez takiej roli dostaje powitanie
 * z dwiema drogami: dołączenie kodem albo założenie organizacji.
 */
export function StartView() {
  const roles = useAuthStore((state) => state.roleAssignments)
  const router = useRouter()
  const { data: session } = useSession()
  const target = roles ? landingPath(roles) : null

  useEffect(() => {
    if (target) router.replace(target)
  }, [target, router])

  if (!roles || target) {
    return (
      <div className="space-y-4" aria-busy="true">
        <Skeleton className="h-8 w-56" />
        <Skeleton className="h-40 w-full rounded-xl" />
      </div>
    )
  }

  const pending = roles
    .filter(
      (r) => r.scopeType === "Tournament" && r.scopeId && ["Viewer", "Coach"].includes(r.role),
    )
    .map((r) => ({ id: r.scopeId!, role: r.role }))
  const firstName = session?.user?.name?.split(" ")[0]

  return (
    <div className="mx-auto max-w-2xl space-y-6">
      <div className="space-y-1">
        <h1 className="text-2xl font-semibold tracking-tight">
          Witaj w Turniejo{firstName ? `, ${firstName}` : ""}!
        </h1>
        <p className="text-muted-foreground">Od czego zaczynamy?</p>
      </div>

      {pending.length > 0 && <PendingMemberships tournamentIds={pending} />}

      <div className="grid gap-4 sm:grid-cols-2">
        <Card>
          <CardHeader>
            <CardTitle className="flex items-center gap-2 text-base">
              <Ticket className="text-primary size-4" />
              Mam kod od organizatora
            </CardTitle>
            <CardDescription>
              Rodzic, trener albo sędzia — kod (albo link) dostaniesz od organizatora turnieju.
            </CardDescription>
          </CardHeader>
          <CardContent>
            <Button asChild className="w-full">
              <Link href="/join">Dołącz kodem</Link>
            </Button>
          </CardContent>
        </Card>
        <Card>
          <CardHeader>
            <CardTitle className="flex items-center gap-2 text-base">
              <Trophy className="text-primary size-4" />
              Organizuję turnieje
            </CardTitle>
            <CardDescription>
              Załóż organizację i poprowadź turniej: drużyny, terminarz, wyniki na żywo.
            </CardDescription>
          </CardHeader>
          <CardContent>
            <BecomeOrganizerDialog
              trigger={
                <Button variant="outline" className="w-full">
                  Zostań organizatorem
                </Button>
              }
            />
          </CardContent>
        </Card>
      </div>
    </div>
  )
}
