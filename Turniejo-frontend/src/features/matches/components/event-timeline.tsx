"use client"

import { Loader2, Undo2 } from "lucide-react"

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
import { cn } from "@/lib/utils"
import { EVENT_LABELS, type MatchEvent, type MatchLivePanel, isGoal } from "@/types/matches"

// Oś czasu pokazuje to, co widzi kibic: gole i kartki (start/pauza są w statusie meczu).
const VISIBLE = new Set(["Goal", "OwnGoal", "PenaltyGoal", "YellowCard", "RedCard"])

function EventIcon({ type }: { type: string }) {
  if (type === "YellowCard")
    return <span className="h-4 w-3 rounded-xs bg-yellow-400" aria-hidden />
  if (type === "RedCard") return <span className="h-4 w-3 rounded-xs bg-red-500" aria-hidden />
  return <span aria-hidden>⚽</span>
}

export function playerLabel(panel: MatchLivePanel, playerId: string | null) {
  if (!playerId) return null
  const player = [...panel.homeTeam.players, ...panel.awayTeam.players].find(
    (p) => p.playerId === playerId,
  )
  if (!player) return null
  const name = `${player.firstName} ${player.lastName}`
  return player.shirtNumber != null ? `${player.shirtNumber}. ${name}` : name
}

export function EventTimeline({
  panel,
  canEdit,
  onRevert,
}: {
  panel: MatchLivePanel
  canEdit: boolean
  onRevert: (event: MatchEvent) => void
}) {
  // Najnowsze na górze według minuty meczu (i sekundy), nie według kolejności wpisania — gol z 12.
  // minuty dopisany po fakcie ląduje pod golem z 30. minuty. Przy tej samej minucie decyduje czas wpisu.
  const events = panel.events
    .filter((e) => VISIBLE.has(e.type))
    .sort(
      (a, b) =>
        b.minute - a.minute ||
        (b.second ?? 0) - (a.second ?? 0) ||
        b.occurredAt.localeCompare(a.occurredAt),
    )

  if (events.length === 0) {
    return <p className="text-muted-foreground py-6 text-center text-sm">Brak zdarzeń</p>
  }

  return (
    <ol className="divide-y" aria-label="Zdarzenia meczu">
      {events.map((event) => {
        const pending = event.id.startsWith("temp-")
        const team =
          event.tournamentTeamId === panel.homeTeam.tournamentTeamId
            ? panel.homeTeam
            : panel.awayTeam
        const player = playerLabel(panel, event.playerId)
        const assist = playerLabel(panel, event.relatedPlayerId)
        return (
          <li
            key={event.id}
            className={cn("flex items-center gap-3 py-3", pending && "opacity-60")}
          >
            <span className="w-9 shrink-0 text-right font-mono text-sm tabular-nums">
              {event.minute}&apos;
            </span>
            <EventIcon type={event.type} />
            <div className="min-w-0 flex-1">
              <p className="truncate text-sm font-medium">
                {EVENT_LABELS[event.type]}
                {player && <span className="font-normal"> · {player}</span>}
              </p>
              <p className="text-muted-foreground truncate text-xs">
                {team.name}
                {isGoal(event.type) && assist && ` · asysta: ${assist}`}
              </p>
            </div>
            {pending ? (
              <Loader2
                className="text-muted-foreground size-4 animate-spin"
                aria-label="Zapisywanie"
              />
            ) : (
              canEdit && (
                <AlertDialog>
                  <AlertDialogTrigger asChild>
                    <Button
                      size="icon"
                      variant="ghost"
                      className="size-10"
                      aria-label={`Cofnij: ${EVENT_LABELS[event.type]} ${event.minute}'`}
                    >
                      <Undo2 />
                    </Button>
                  </AlertDialogTrigger>
                  <AlertDialogContent>
                    <AlertDialogHeader>
                      <AlertDialogTitle>Cofnąć zdarzenie?</AlertDialogTitle>
                      <AlertDialogDescription>
                        {EVENT_LABELS[event.type]} ({event.minute}&apos;
                        {player ? `, ${player}` : ""}) zniknie z meczu
                        {isGoal(event.type) ? ", a wynik zostanie przeliczony" : ""}.
                      </AlertDialogDescription>
                    </AlertDialogHeader>
                    <AlertDialogFooter>
                      <AlertDialogCancel>Anuluj</AlertDialogCancel>
                      <AlertDialogAction onClick={() => onRevert(event)}>Cofnij</AlertDialogAction>
                    </AlertDialogFooter>
                  </AlertDialogContent>
                </AlertDialog>
              )
            )}
          </li>
        )
      })}
    </ol>
  )
}
