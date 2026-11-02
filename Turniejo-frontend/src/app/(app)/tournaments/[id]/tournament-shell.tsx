"use client"

import { CalendarDays, SearchX, ShieldAlert } from "lucide-react"
import Link from "next/link"
import { usePathname } from "next/navigation"
import { type ReactNode, useEffect } from "react"

import { EmptyState } from "@/components/layout/empty-state"
import { Button } from "@/components/ui/button"
import { Skeleton } from "@/components/ui/skeleton"
import { MyTournamentRole } from "@/features/tournaments/components/my-role-badge"
import { TournamentStatusBadge } from "@/features/tournaments/components/status-badge"
import { useTournamentContext } from "@/features/tournaments/use-tournament-context"
import { isApiError } from "@/lib/api/errors"
import { formatDateRange } from "@/lib/format"
import { cn } from "@/lib/utils"
import { useActiveTournamentStore } from "@/stores/active-tournament-store"

const TABS = [
  { segment: "dashboard", label: "Przegląd" },
  { segment: "schedule", label: "Terminarz" },
  { segment: "teams", label: "Drużyny" },
  { segment: "categories", label: "Kategorie i grupy" },
  { segment: "pitches", label: "Boiska" },
  { segment: "bracket", label: "Drabinka" },
  { segment: "settings", label: "Ustawienia" },
]

function TournamentTabs({ id }: { id: string }) {
  const pathname = usePathname()
  const { tournament } = useTournamentContext()
  // Puchar nie ma grup — zakładka to po prostu kategorie.
  const knockout = tournament.data?.format === "Knockout"
  return (
    <nav aria-label="Sekcje turnieju" className="-mx-4 overflow-x-auto px-4 lg:mx-0 lg:px-0">
      <ul className="flex min-w-max gap-1 border-b">
        {TABS.map(({ segment, label: baseLabel }) => {
          const label = segment === "categories" && knockout ? "Kategorie" : baseLabel
          const href = `/tournaments/${id}/${segment}`
          const active = pathname.startsWith(href)
          return (
            <li key={segment}>
              <Link
                href={href}
                aria-current={active ? "page" : undefined}
                className={cn(
                  "-mb-px block border-b-2 px-3 py-2 text-sm font-medium transition-colors",
                  active
                    ? "border-primary text-primary"
                    : "text-muted-foreground hover:text-foreground border-transparent",
                )}
              >
                {label}
              </Link>
            </li>
          )
        })}
      </ul>
    </nav>
  )
}

export function TournamentShell({ children }: { children: ReactNode }) {
  const { id, tournament, roles } = useTournamentContext()
  const activeId = useActiveTournamentStore((state) => state.tournament?.id)
  const setActiveTournament = useActiveTournamentStore((state) => state.setTournament)

  // Wejście w turniej (także z linku) ustawia go jako aktywny kontekst nawigacji.
  const name = tournament.data?.name
  const format = tournament.data?.format
  useEffect(() => {
    if (name) setActiveTournament({ id, name, format })
  }, [id, name, format, setActiveTournament])

  // Brak dostępu albo turniej usunięty: nie zostawiamy go jako aktywnego w nagłówku.
  const errorStatus = isApiError(tournament.error) ? tournament.error.status : 0
  const lostAccess = errorStatus === 403 || errorStatus === 404
  useEffect(() => {
    if (lostAccess && activeId?.toLowerCase() === id.toLowerCase()) setActiveTournament(null)
  }, [lostAccess, activeId, id, setActiveTournament])

  // isPending, nie isLoading: przed załadowaniem sesji zapytanie jest wyłączone (isLoading = false).
  if (tournament.isPending) {
    return (
      <div className="space-y-4">
        <Skeleton className="h-8 w-64" />
        <Skeleton className="h-10 w-full" />
        <Skeleton className="h-48 w-full" />
      </div>
    )
  }

  if (tournament.isError) {
    const status = errorStatus
    return (
      <EmptyState
        icon={status === 403 ? ShieldAlert : SearchX}
        title={status === 403 ? "Nie masz dostępu do tego turnieju" : "Nie znaleziono turnieju"}
        description={
          status === 403
            ? "Dołącz do turnieju kodem albo poproś organizatora o dostęp."
            : "Turniej mógł zostać usunięty albo link jest nieprawidłowy."
        }
        action={
          <Button asChild variant="outline">
            <Link href="/tournaments">Wróć do listy</Link>
          </Button>
        }
      />
    )
  }

  const data = tournament.data!
  return (
    <div className="space-y-6">
      <header className="space-y-3">
        <div className="flex flex-wrap items-center gap-3">
          <h1 className="text-2xl font-semibold tracking-tight">{data.name}</h1>
          <TournamentStatusBadge status={data.status} />
        </div>
        <p className="text-muted-foreground flex items-center gap-2 text-sm">
          <CalendarDays className="size-4" />
          {formatDateRange(data.startAt, data.endAt)}
        </p>
        <MyTournamentRole roles={roles} tournamentId={id} />
        <TournamentTabs id={id} />
      </header>
      {children}
    </div>
  )
}
