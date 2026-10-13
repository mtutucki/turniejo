"use client"

import { Trash2 } from "lucide-react"
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
import { APP_TIME_ZONE } from "@/lib/format"
import { cn } from "@/lib/utils"
import { useActiveTournamentStore } from "@/stores/active-tournament-store"
import {
  TOURNAMENT_FORMAT_LABELS,
  type Tournament,
  type TournamentStatus,
} from "@/types/tournaments"

import { tournamentKeys, useDeleteTournament } from "../api"
import { TournamentStatusBadge } from "./status-badge"

/** Usuń anulowany turniej — przycisk na karcie (poza linkiem), z potwierdzeniem. */
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
          className="text-muted-foreground hover:text-destructive absolute right-2 bottom-2"
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

const dayMonthFormatter = new Intl.DateTimeFormat("pl-PL", {
  day: "numeric",
  month: "short",
  year: "numeric",
  timeZone: APP_TIME_ZONE,
})

function dateParts(value: string) {
  const parts = dayMonthFormatter.formatToParts(new Date(value))
  const part = (type: string) => parts.find((p) => p.type === type)?.value ?? ""
  return { day: part("day"), month: part("month").replace(".", ""), year: part("year") }
}

/** Druga linia karty: format i to, czego nie widać na kafelku z datą (koniec turnieju, inny rok). */
function metaLine(tournament: Tournament) {
  const start = dateParts(tournament.startAt)
  const end = dateParts(tournament.endAt)
  const sameDay = start.day === end.day && start.month === end.month && start.year === end.year
  const thisYear = String(new Date().getFullYear())
  return [
    tournament.format && TOURNAMENT_FORMAT_LABELS[tournament.format],
    sameDay ? "jednodniowy" : `do ${end.day} ${end.month}`,
    end.year !== thisYear && end.year,
  ]
    .filter(Boolean)
    .join(" · ")
}

function TournamentCard({ tournament, roleLabel }: TournamentListItem) {
  const setTournament = useActiveTournamentStore((state) => state.setTournament)
  // Kody dostępu backend zwraca tylko organizatorowi — tylko on może usunąć turniej.
  const deletable = tournament.status === "Cancelled" && tournament.refereeCode !== null
  const live = tournament.status === "InProgress"
  const start = dateParts(tournament.startAt)
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
    <li className="relative">
      <Link
        href={`/tournaments/${tournament.id}/dashboard`}
        onClick={() =>
          setTournament({ id: tournament.id, name: tournament.name, format: tournament.format })
        }
        {...prefetchHandlers(warmUp)}
        className="focus-visible:ring-ring group block h-full rounded-xl outline-none focus-visible:ring-2"
      >
        <Card
          className={cn(
            "group-hover:border-primary/50 h-full flex-row items-start gap-4 p-4 transition-colors",
            live && "border-primary/40 bg-primary/5",
          )}
        >
          <div
            className={cn(
              "flex size-14 shrink-0 flex-col items-center justify-center rounded-lg",
              live ? "bg-primary text-primary-foreground" : "bg-secondary",
            )}
          >
            <span className="text-xl leading-none font-semibold tabular-nums">{start.day}</span>
            <span
              className={cn(
                "mt-1 text-[11px] leading-none font-medium tracking-wide uppercase",
                !live && "text-muted-foreground",
              )}
            >
              {start.month}
            </span>
          </div>
          <div className="min-w-0 flex-1 space-y-2">
            <div className="space-y-0.5">
              <p className="truncate leading-tight font-semibold">{tournament.name}</p>
              <p className="text-muted-foreground truncate text-sm">{metaLine(tournament)}</p>
            </div>
            <div className="flex flex-wrap items-center gap-1.5">
              <TournamentStatusBadge status={tournament.status} />
              {roleLabel && <Badge variant="outline">{roleLabel}</Badge>}
            </div>
          </div>
        </Card>
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
  { key: "upcoming", title: "W przygotowaniu" },
  { key: "drafts", title: "Szkice" },
  { key: "past", title: "Zakończone i anulowane" },
]

/**
 * Lista turniejów pogrupowana według tego, co się z nimi dzieje — najpierw trwające, na końcu
 * zamknięte. W grupie najbliższy termin jest na górze; zakończone od najnowszego.
 */
export function TournamentSections({ items }: { items: TournamentListItem[] }) {
  return (
    <div className="space-y-8">
      {SECTIONS.map(({ key, title }) => {
        const inSection = items
          .filter((item) => SECTION_OF[item.tournament.status] === key)
          .sort((a, b) => {
            const order = a.tournament.startAt.localeCompare(b.tournament.startAt)
            return key === "past" ? -order : order
          })
        if (inSection.length === 0) return null
        return (
          <section key={key} className="space-y-3">
            <h2 className="text-sm font-semibold">
              {title}{" "}
              <span className="text-muted-foreground font-normal tabular-nums">
                {inSection.length}
              </span>
            </h2>
            <ul
              className={cn(
                "grid gap-3 sm:grid-cols-2 xl:grid-cols-3 2xl:grid-cols-4",
                key === "past" && "opacity-70",
              )}
            >
              {inSection.map((item) => (
                <TournamentCard key={item.tournament.id} {...item} />
              ))}
            </ul>
          </section>
        )
      })}
    </div>
  )
}
