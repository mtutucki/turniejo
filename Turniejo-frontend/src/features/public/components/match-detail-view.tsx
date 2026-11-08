"use client"

import { ArrowLeft, CalendarClock } from "lucide-react"
import Link from "next/link"

import { EmptyState } from "@/components/layout/empty-state"
import { Button } from "@/components/ui/button"
import { Skeleton } from "@/components/ui/skeleton"
import { EventTimeline } from "@/features/matches/components/event-timeline"
import { resultNote } from "@/features/matches/result-note"
import { MatchStatusBadge } from "@/features/matches/components/match-status-badge"

import { usePublicLiveMatch } from "../api"
import { usePublicContext } from "../public-context"
import { useLiveMinute } from "./public-match-card"
import { AnimatedScore } from "@/features/matches/components/live-score-board"

/** Strona meczu na portalu: wynik, minuta (dla trwającego) i oś czasu goli i kartek. */
export function MatchDetailView({ matchId }: { matchId: string }) {
  const { slug, lookups } = usePublicContext()
  const match = usePublicLiveMatch(slug, matchId, true)
  const minute = useLiveMinute(match.data)

  const back = (
    <Button asChild variant="ghost" size="sm" className="-ml-2">
      <Link href={`/t/${slug}`}>
        <ArrowLeft />
        Mecze
      </Link>
    </Button>
  )

  if (match.isPending) return <Skeleton className="h-80 w-full rounded-xl" />
  if (!match.data) {
    return (
      <div className="space-y-4">
        {back}
        <EmptyState icon={CalendarClock} title="Szczegóły pojawią się po rozpoczęciu meczu" />
      </div>
    )
  }

  const m = match.data
  const live = m.status === "Live" || m.status === "Paused"
  // Nazwy z listy drużyn portalu (respektują ShowTeams); backend i tak zwraca neutralne przy ukryciu.
  const home = lookups.teamName(m.homeTeam.tournamentTeamId, m.homeTeam.name)
  const away = lookups.teamName(m.awayTeam.tournamentTeamId, m.awayTeam.name)

  return (
    <div className="mx-auto max-w-2xl space-y-4">
      {back}
      <section
        aria-label="Wynik"
        className={
          live ? "bg-card border-live rounded-2xl border p-5" : "bg-card rounded-2xl border p-5"
        }
      >
        <div className="mb-4 flex items-center justify-center gap-2">
          <MatchStatusBadge status={m.status} />
          {live && minute != null && (
            <span
              className="text-live font-mono text-sm font-semibold tabular-nums"
              data-testid="live-minute"
            >
              {minute}&apos;
            </span>
          )}
        </div>
        <div className="grid grid-cols-[1fr_auto_1fr] items-center gap-3 text-center">
          <p className="min-w-0 truncate font-semibold">{home}</p>
          <p className="font-mono text-5xl font-bold tabular-nums" data-testid="detail-score">
            <AnimatedScore value={m.homeScore} />:<AnimatedScore value={m.awayScore} />
          </p>
          <p className="min-w-0 truncate font-semibold">{away}</p>
        </div>
        {resultNote(m) && (
          <p className="text-muted-foreground mt-2 text-center text-sm">{resultNote(m)}</p>
        )}
      </section>
      <section aria-label="Zdarzenia" className="bg-card rounded-2xl border px-4 py-2">
        <h2 className="text-muted-foreground py-2 text-xs font-medium tracking-wide uppercase">
          Zdarzenia
        </h2>
        <EventTimeline
          panel={{
            ...m,
            homeTeam: { ...m.homeTeam, name: home },
            awayTeam: { ...m.awayTeam, name: away },
          }}
          canEdit={false}
          onRevert={() => {}}
        />
      </section>
    </div>
  )
}
