"use client"

import { ChevronRight, Clock, MapPin } from "lucide-react"
import Link from "next/link"

import { Card, CardContent } from "@/components/ui/card"
import { prefetchHandlers, usePrefetch } from "@/hooks/use-prefetch"
import { formatDateTime } from "@/lib/format"
import { cn } from "@/lib/utils"
import type { Match } from "@/types/matches"

import { liveKeys } from "../live-api"
import { MatchStatusBadge } from "./match-status-badge"

type RefereeMatchCardProps = {
  match: Match
  homeName: string
  awayName: string
  pitchName: string | null
  tournamentName: string | null
}

/** Mecz sędziego: status, drużyny, boisko, godzina — cała karta prowadzi do panelu live. */
export function RefereeMatchCard({
  match,
  homeName,
  awayName,
  pitchName,
  tournamentName,
}: RefereeMatchCardProps) {
  const live = match.status === "Live" || match.status === "Paused"
  const prefetch = usePrefetch()
  const warmUp = () =>
    prefetch(
      { queryKey: liveKeys.location(match.id), path: `/api/matches/${match.id}/location` },
      {
        queryKey: liveKeys.panel(match.id),
        path: `/api/tournaments/${match.tournamentId}/matches/${match.id}/live-panel`,
      },
    )
  const showScore = live || match.status === "Finished"
  return (
    <Link
      href={`/matches/${match.id}/live`}
      {...prefetchHandlers(warmUp)}
      className="focus-visible:ring-ring block rounded-xl outline-none focus-visible:ring-2"
    >
      <Card
        className={cn("hover:border-primary/50 py-4 transition-colors", live && "border-live/60")}
      >
        <CardContent className="flex items-center gap-4 px-4">
          <div className="min-w-0 flex-1 space-y-2">
            <div className="flex flex-wrap items-center gap-2 text-xs">
              <MatchStatusBadge status={match.status} />
              {tournamentName && (
                <span className="text-muted-foreground truncate">{tournamentName}</span>
              )}
            </div>
            <p className="flex items-center gap-2 font-medium">
              <span className="min-w-0 truncate">{homeName}</span>
              {showScore ? (
                <span className="shrink-0 font-mono tabular-nums">
                  {match.homeScore}:{match.awayScore}
                </span>
              ) : (
                <span className="text-muted-foreground shrink-0 text-sm">vs</span>
              )}
              <span className="min-w-0 truncate">{awayName}</span>
            </p>
            <p className="text-muted-foreground flex flex-wrap gap-x-4 gap-y-1 text-xs">
              <span className="flex items-center gap-1 font-mono tabular-nums">
                <Clock className="size-3" />
                {formatDateTime(match.scheduledAt)}
              </span>
              <span className="flex items-center gap-1">
                <MapPin className="size-3" />
                {pitchName ?? "Bez boiska"}
              </span>
            </p>
          </div>
          <ChevronRight className="text-muted-foreground size-5 shrink-0" />
        </CardContent>
      </Card>
    </Link>
  )
}
