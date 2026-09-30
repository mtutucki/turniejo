"use client"

import { CalendarDays, KeyRound, Trash2, UserRound, Users } from "lucide-react"
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
import { Button } from "@/components/ui/button"
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card"
import { prefetchHandlers, usePrefetch } from "@/hooks/use-prefetch"
import { formatDateRange } from "@/lib/format"
import { cn } from "@/lib/utils"
import { useActiveTournamentStore } from "@/stores/active-tournament-store"
import { TOURNAMENT_FORMAT_LABELS, type Tournament } from "@/types/tournaments"

import { tournamentKeys, useDeleteTournament } from "../api"
import { TournamentStatusBadge } from "./status-badge"

/** Usuń anulowany turniej — przycisk obok karty (nie w linku), z potwierdzeniem. */
function DeleteCancelledTournament({ tournament }: { tournament: Tournament }) {
  const remove = useDeleteTournament(tournament.id)
  const active = useActiveTournamentStore((state) => state.tournament)
  const setTournament = useActiveTournamentStore((state) => state.setTournament)

  return (
    <AlertDialog>
      <AlertDialogTrigger asChild>
        <Button
          variant="ghost"
          size="sm"
          className="text-muted-foreground hover:text-destructive absolute right-3 bottom-3"
          aria-label={`Usuń turniej ${tournament.name}`}
        >
          <Trash2 />
          Usuń
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
            className="bg-destructive hover:bg-destructive/90 text-white"
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

export function TournamentCard({
  tournament,
  roleLabel,
}: {
  tournament: Tournament
  /** Rola usera w turnieju, do którego dołączył kodem (lista "Dołączone turnieje"). */
  roleLabel?: string
}) {
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
    <div className="relative">
      <Link
        href={`/tournaments/${tournament.id}/dashboard`}
        onClick={() => setTournament({ id: tournament.id, name: tournament.name })}
        {...prefetchHandlers(warmUp)}
        className="focus-visible:ring-ring group block h-full rounded-xl outline-none focus-visible:ring-2"
      >
        <Card className="group-hover:border-primary/50 h-full gap-4 transition-colors">
          <CardHeader className="flex flex-row items-start justify-between gap-3">
            <CardTitle className="min-w-0 truncate text-base">{tournament.name}</CardTitle>
            <TournamentStatusBadge status={tournament.status} className="shrink-0" />
          </CardHeader>
          <CardContent
            className={cn("text-muted-foreground space-y-2 text-sm", deletable && "pb-10")}
          >
            <p className="flex items-center gap-2">
              <CalendarDays className="size-4" />
              {formatDateRange(tournament.startAt, tournament.endAt)}
            </p>
            <p className="flex items-center gap-2">
              <Users className="size-4" />
              Do {tournament.teamLimit} drużyn
              {tournament.format && ` · ${TOURNAMENT_FORMAT_LABELS[tournament.format]}`}
            </p>
            {roleLabel && (
              <p className="flex items-center gap-2">
                <UserRound className="size-4" />
                {roleLabel}
              </p>
            )}
            {tournament.refereeCode && tournament.coachCode && (
              <p className="flex items-center gap-2">
                <KeyRound className="size-4" />
                <span>
                  Sędzia <code className="text-foreground font-mono">{tournament.refereeCode}</code>{" "}
                  · Trener <code className="text-foreground font-mono">{tournament.coachCode}</code>
                </span>
              </p>
            )}
          </CardContent>
        </Card>
      </Link>
      {deletable && <DeleteCancelledTournament tournament={tournament} />}
    </div>
  )
}
