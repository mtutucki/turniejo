import { CalendarClock } from "lucide-react"

import { EmptyState } from "@/components/layout/empty-state"
import { Skeleton } from "@/components/ui/skeleton"
import { MatchStatusBadge } from "@/features/matches/components/match-status-badge"
import { formatDateTime } from "@/lib/format"
import { cn } from "@/lib/utils"
import type { MatchStatus } from "@/types/matches"

type SimpleMatch = {
  matchId: string
  scheduledAt: string
  status: MatchStatus
  homeTeamName: string
  awayTeamName: string
  homeScore: number
  awayScore: number
  tournamentName?: string
}

/** Lista meczów w panelach rodzica i trenera: godzina, drużyny, wynik; wyróżniona drużyna "moja". */
export function SimpleMatchList({
  matches,
  isPending,
  highlightTeam,
  emptyTitle,
}: {
  matches: SimpleMatch[] | undefined
  isPending: boolean
  /** Nazwa drużyny dziecka/trenera — pogrubiona na liście. */
  highlightTeam?: string
  emptyTitle: string
}) {
  if (isPending) return <Skeleton className="h-40 w-full rounded-xl" />
  if (!matches?.length) return <EmptyState icon={CalendarClock} title={emptyTitle} />

  const showScore = (m: SimpleMatch) =>
    m.status === "Live" || m.status === "Paused" || m.status === "Finished"

  return (
    <ul className="bg-card divide-y rounded-xl border">
      {matches.map((m) => (
        <li key={m.matchId} className="flex flex-col gap-2 px-4 py-3 sm:flex-row sm:items-center">
          <div className="text-muted-foreground flex w-44 shrink-0 flex-wrap items-center gap-2 text-xs">
            <span className="text-foreground font-mono tabular-nums">
              {formatDateTime(m.scheduledAt)}
            </span>
            {m.status !== "Scheduled" && <MatchStatusBadge status={m.status} />}
          </div>
          <p className="flex min-w-0 flex-1 items-center gap-2">
            <span
              className={cn(
                "min-w-0 truncate",
                m.homeTeamName === highlightTeam && "font-semibold",
              )}
            >
              {m.homeTeamName}
            </span>
            <span className="text-muted-foreground shrink-0 font-mono text-sm tabular-nums">
              {showScore(m) ? `${m.homeScore}:${m.awayScore}` : "vs"}
            </span>
            <span
              className={cn(
                "min-w-0 truncate",
                m.awayTeamName === highlightTeam && "font-semibold",
              )}
            >
              {m.awayTeamName}
            </span>
          </p>
          {m.tournamentName && (
            <span className="text-muted-foreground truncate text-xs">{m.tournamentName}</span>
          )}
        </li>
      ))}
    </ul>
  )
}
