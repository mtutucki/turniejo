"use client"

import { ChevronRight, Trash2 } from "lucide-react"
import { useQueryClient } from "@tanstack/react-query"
import Link from "next/link"
import { toast } from "sonner"

import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
  AlertDialogTrigger,
} from "@/components/ui/alert-dialog"
import { Badge } from "@/components/ui/badge"
import { Button } from "@/components/ui/button"
import { Card } from "@/components/ui/card"
import { prefetchHandlers, usePrefetch } from "@/hooks/use-prefetch"
import { formatDateRange } from "@/lib/format"
import { cn } from "@/lib/utils"
import { useActiveTournamentStore } from "@/stores/active-tournament-store"
import {
  TOURNAMENT_FORMAT_LABELS,
  type Tournament,
  type TournamentStatus,
} from "@/types/tournaments"

import { tournamentKeys, useDeleteTournament } from "../api"
import { TournamentStatusBadge } from "./status-badge"

/** Usuń anulowany turniej — przycisk obok wiersza (nie w linku), z potwierdzeniem. */
function DeleteCancelledTournament({ tournament }: { tournament: Tournament }) {
  const remove = useDeleteTournament(tournament.id)
  const active = useActiveTournamentStore((state) => state.tournament)
  const setTournament = useActiveTournamentStore((state) => state.setTournament)

  return (
    <AlertDialog>
      <AlertDialogTrigger asChild>
        <Button
          variant="ghost"
          size="icon"
          className="text-muted-foreground hover:text-destructive mr-2 shrink-0"
          aria-label={`Usuń turniej ${tournament.name}`}
          title="Usuń turniej"
        >
          <Trash2 />
        </Button>
      </AlertDialogTrigger>
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle>Usunąć turniej „{tournament.name}”?</AlertDialogTitle>
          <AlertDialogDescription>
            Turniej jest anulowany. Zniknie z listy, a jego strona i portal kibica przestaną być
            dostępne. Tej operacji nie da się cofnąć z poziomu aplikacji.
          </AlertDialogDescription>
        </AlertDialogHeader>
        <AlertDialogFooter>
          <AlertDialogCancel>Anuluj</AlertDialogCancel>
          <AlertDialogAction
            variant="destructive"
            onClick={() =>
              remove.mutate(undefined, {
                onSuccess: () => {
                  // Usunięty turniej nie może zostać "aktywnym" w nagłówku.
                  if (active?.id === tournament.id) setTournament(null)
                  toast.success(`Usunięto „${tournament.name}”`)
                },
              })
            }
          >
            Usuń turniej
          </AlertDialogAction>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  )
}

export type TournamentListItem = {
  tournament: Tournament
  /** Rola usera w turnieju, do którego dołączył kodem (lista "Dołączone turnieje"). */
  roleLabel?: string
}

function TournamentRow({ tournament, roleLabel }: TournamentListItem) {
  const setTournament = useActiveTournamentStore((state) => state.setTournament)
  // Kody dostępu backend zwraca tylko organizatorowi — tylko on może usunąć turniej.
  const deletable = tournament.status === "Cancelled" && tournament.refereeCode !== null
  const queryClient = useQueryClient()
  const prefetch = usePrefetch()
  const warmUp = () => {
    // Turniej już mamy z listy — wkładamy go do cache od razu; dashboard pobieramy w tle.
    if (!queryClient.getQueryData(tournamentKeys.detail(tournament.id))) {
      queryClient.setQueryData(tournamentKeys.detail(tournament.id), tournament)
    }
    prefetch({
      queryKey: tournamentKeys.dashboard(tournament.id),
      path: `/api/tournaments/${tournament.id}/dashboard?upcomingMatchesCount=5`,
    })
  }

  return (
    <li className="flex items-center">
      <Link
        href={`/tournaments/${tournament.id}/dashboard`}
        onClick={() =>
          setTournament({ id: tournament.id, name: tournament.name, format: tournament.format })
        }
        {...prefetchHandlers(warmUp)}
        className="hover:bg-accent/50 focus-visible:ring-ring flex min-w-0 flex-1 items-center gap-3 px-4 py-3 outline-none focus-visible:ring-2 focus-visible:ring-inset"
      >
        <div className="min-w-0 flex-1 space-y-1">
          <p className="flex flex-wrap items-center gap-x-2 gap-y-1">
            <span className="min-w-0 truncate font-medium">{tournament.name}</span>
            {roleLabel && <Badge variant="outline">{roleLabel}</Badge>}
          </p>
          <p className="text-muted-foreground text-sm">
            <span className="font-mono tabular-nums">
              {formatDateRange(tournament.startAt, tournament.endAt)}
            </span>
            {tournament.format && ` · ${TOURNAMENT_FORMAT_LABELS[tournament.format]}`}
          </p>
        </div>
        <TournamentStatusBadge status={tournament.status} className="shrink-0" />
        <ChevronRight className="text-muted-foreground size-4 shrink-0" />
      </Link>
      {deletable && <DeleteCancelledTournament tournament={tournament} />}
    </li>
  )
}

type SectionKey = "live" | "upcoming" | "drafts" | "past"

const SECTION_OF: Record<TournamentStatus, SectionKey> = {
  InProgress: "live",
  RegistrationOpen: "upcoming",
  RegistrationClosed: "upcoming",
  ScheduleGenerated: "upcoming",
  Draft: "drafts",
  Finished: "past",
  Cancelled: "past",
}

const SECTIONS: { key: SectionKey; title: string }[] = [
  { key: "live", title: "Trwają" },
  { key: "upcoming", title: "Nadchodzące" },
  { key: "drafts", title: "Szkice" },
  { key: "past", title: "Zakończone i anulowane" },
]

/**
 * Lista turniejów pogrupowana według tego, co się z nimi dzieje — najpierw trwające, na końcu
 * zamknięte. W grupie najbliższy termin jest na górze; zakończone od najnowszego.
 */
export function TournamentSections({ items }: { items: TournamentListItem[] }) {
  return (
    <div className="space-y-6">
      {SECTIONS.map(({ key, title }) => {
        const inSection = items
          .filter((item) => SECTION_OF[item.tournament.status] === key)
          .sort((a, b) => {
            const order = a.tournament.startAt.localeCompare(b.tournament.startAt)
            return key === "past" ? -order : order
          })
        if (inSection.length === 0) return null
        return (
          <section key={key} className="space-y-2">
            <h2 className="text-muted-foreground text-xs font-medium tracking-wide uppercase">
              {title} <span className="font-mono tabular-nums">({inSection.length})</span>
            </h2>
            <Card className={cn("gap-0 overflow-hidden py-0", key === "past" && "opacity-80")}>
              <ul className="divide-y">
                {inSection.map((item) => (
                  <TournamentRow key={item.tournament.id} {...item} />
                ))}
              </ul>
            </Card>
          </section>
        )
      })}
    </div>
  )
}
