"use client"

import { MapPin } from "lucide-react"
import Link from "next/link"
import { useEffect, useState } from "react"

import { MatchStatusBadge } from "@/features/matches/components/match-status-badge"
import { formatTime } from "@/lib/format"
import { cn } from "@/lib/utils"
import type { PublicMatch } from "@/types/public"

import { type PublicLiveMatch, usePublicLiveMatch } from "../api"
import { usePublicContext } from "../public-context"
import { AnimatedScore } from "@/features/matches/components/live-score-board"

/** Minuta meczu: czas gry z serwera + upływ od odpowiedzi (odświeżane co 15 s — wystarcza do minuty). */
export function useLiveMinute(live: PublicLiveMatch | undefined) {
  const [now, setNow] = useState(() => Date.now())
  const running = live?.status === "Live"
  useEffect(() => {
    if (!running) return
    const timer = setInterval(() => setNow(Date.now()), 15_000)
    return () => clearInterval(timer)
  }, [running])

  if (live?.elapsedSeconds == null) return null
  const seconds = live.elapsedSeconds + (running ? Math.max(0, (now - live.receivedAt) / 1000) : 0)
  return Math.floor(seconds / 60) + 1
}

function TeamLine({
  name,
  score,
  winner,
  showScore,
}: {
  name: string
  score: number
  winner: boolean
  showScore: boolean
}) {
  return (
    <div className="flex items-center justify-between gap-3">
      <span className={cn("min-w-0 truncate", winner && "font-semibold")}>{name}</span>
      {showScore && (
        <span
          className={cn(
            "font-mono text-lg font-semibold tabular-nums",
            !winner && "text-muted-foreground",
          )}
        >
          <AnimatedScore value={score} />
        </span>
      )}
    </div>
  )
}

export function PublicMatchCard({ match }: { match: PublicMatch }) {
  const { slug, tournament, lookups } = usePublicContext()
  const isLive = match.status === "Live" || match.status === "Paused"
  const live = usePublicLiveMatch(slug, match.id, isLive && tournament.settings.showLiveMatches)
  const minute = useLiveMinute(live.data)
  const showScore = isLive || match.status === "Finished"
  const home = lookups.teamName(match.homeTournamentTeamId, "Gospodarze")
  const away = lookups.teamName(match.awayTournamentTeamId, "Goście")
  const pitch = lookups.pitchName(match.pitchId)
  // Mecze pucharowe nie mają grupy.
  const group =
    lookups.groupName(match.groupId) ?? (match.stageType !== "Group" ? "Faza pucharowa" : null)
  const penalties = match.homePenaltyScore != null && match.awayPenaltyScore != null
  const homeWins =
    match.homeScore > match.awayScore ||
    (penalties &&
      match.homeScore === match.awayScore &&
      match.homePenaltyScore! > match.awayPenaltyScore!)
  const awayWins =
    match.awayScore > match.homeScore ||
    (penalties &&
      match.homeScore === match.awayScore &&
      match.awayPenaltyScore! > match.homePenaltyScore!)
  // Szczegóły (strzelcy, kartki) mają mecze trwające i zakończone.
  const hasDetails = showScore

  const card = (
    <article
      aria-label={`${home} – ${away}`}
      data-status={match.status}
      className={cn(
        "bg-card rounded-xl border p-3",
        isLive && "border-live shadow-[0_0_0_1px_var(--live)]",
      )}
    >
      <header className="text-muted-foreground mb-2 flex items-center justify-between gap-2 text-xs">
        <span className="flex min-w-0 items-center gap-2">
          {isLive ? (
            <span className="text-live flex items-center gap-1.5 font-semibold">
              <span className="relative flex size-2">
                {match.status === "Live" && (
                  <span className="bg-live absolute inline-flex size-full animate-ping rounded-full opacity-75" />
                )}
                <span className="bg-live relative inline-flex size-2 rounded-full" />
              </span>
              {match.status === "Paused" ? "PRZERWA" : "LIVE"}
              {minute != null && (
                <span className="font-mono tabular-nums" data-testid="live-minute">
                  {minute}&apos;
                </span>
              )}
            </span>
          ) : (
            <span className="text-foreground font-mono tabular-nums">
              {formatTime(match.scheduledAt)}
            </span>
          )}
          {group && <span className="truncate">{group}</span>}
        </span>
        <span className="flex shrink-0 items-center gap-2">
          {pitch && (
            <span className="flex items-center gap-1">
              <MapPin className="size-3" />
              {pitch}
            </span>
          )}
          {!isLive && match.status !== "Scheduled" && <MatchStatusBadge status={match.status} />}
        </span>
      </header>
      <div className="space-y-1" data-testid="match-score">
        <TeamLine
          name={home}
          score={match.homeScore}
          showScore={showScore}
          winner={showScore && homeWins}
        />
        <TeamLine
          name={away}
          score={match.awayScore}
          showScore={showScore}
          winner={showScore && awayWins}
        />
      </div>
      {penalties && (
        <p className="text-muted-foreground mt-1 text-right text-xs">
          karne{" "}
          <span className="font-mono tabular-nums">
            {match.homePenaltyScore}:{match.awayPenaltyScore}
          </span>
        </p>
      )}
    </article>
  )

  return hasDetails ? (
    <Link
      href={`/t/${slug}/matches/${match.id}`}
      className="focus-visible:ring-ring block rounded-xl outline-none hover:opacity-90 focus-visible:ring-2"
    >
      {card}
    </Link>
  ) : (
    card
  )
}
