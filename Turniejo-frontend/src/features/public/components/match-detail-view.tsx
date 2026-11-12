"use client"

import { ArrowLeft, CalendarClock } from "lucide-react"
import Link from "next/link"

import { EmptyState } from "@/components/layout/empty-state"
import { TeamLogo } from "@/features/teams/components/team-logo"
import { Button } from "@/components/ui/button"
import { Skeleton } from "@/components/ui/skeleton"
import { EventTimeline, playerLabel } from "@/features/matches/components/event-timeline"
import { resultNote } from "@/features/matches/result-note"
import { MatchStatusBadge } from "@/features/matches/components/match-status-badge"

import { usePublicLiveMatch } from "../api"
import { usePublicContext } from "../public-context"
import { useLiveMinute } from "./public-match-card"
import { AnimatedScore } from "@/features/matches/components/live-score-board"

/** Strona meczu na portalu: wynik, minuta (dla trwającego) i oś czasu goli i kartek. */
function TeamLink({
  slug,
  id,
  show,
  logoUrl,
  children,
}: {
  slug: string
  id: string
  show: boolean
  logoUrl?: string | null
  children: string
}) {
  if (!show) return <p className="min-w-0 truncate font-semibold">{children}</p>
  // Przy „Pokazuj drużyny” nad nazwą herb (albo inicjały), jak w nagłówku meczu w telewizji.
  return (
    <Link
      href={`/t/${slug}/teams/${id}`}
      className="flex min-w-0 flex-col items-center gap-2 font-semibold hover:underline"
    >
      <TeamLogo name={children} logoUrl={logoUrl ?? null} size={48} />
      <span className="max-w-full truncate">{children}</span>
    </Link>
  )
}

export function MatchDetailView({ matchId }: { matchId: string }) {
  const { slug, tournament, lookups } = usePublicContext()
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
  const mvpEvent = m.events.filter((e) => e.type === "MvpSelected").at(-1)
  const mvp = mvpEvent ? playerLabel(m, mvpEvent.playerId) : null
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
          <MatchStatusBadge status={m.status} awaitingDecision={m.awaitingDecision} />
          {live && !m.awaitingDecision && minute != null && (
            <span
              className="text-live font-mono text-sm font-semibold tabular-nums"
              data-testid="live-minute"
            >
              {minute}&apos;
            </span>
          )}
        </div>
        <div className="grid grid-cols-[1fr_auto_1fr] items-center gap-3 text-center">
          <TeamLink
            slug={slug}
            id={m.homeTeam.tournamentTeamId}
            show={tournament.settings.showTeams}
            logoUrl={lookups.teamLogo(m.homeTeam.tournamentTeamId)}
          >
            {home}
          </TeamLink>
          <p className="font-mono text-5xl font-bold tabular-nums" data-testid="detail-score">
            <AnimatedScore value={m.homeScore} />:<AnimatedScore value={m.awayScore} />
          </p>
          <TeamLink
            slug={slug}
            id={m.awayTeam.tournamentTeamId}
            show={tournament.settings.showTeams}
            logoUrl={lookups.teamLogo(m.awayTeam.tournamentTeamId)}
          >
            {away}
          </TeamLink>
        </div>
        {resultNote(m) && (
          <p className="text-muted-foreground mt-2 text-center text-sm">{resultNote(m)}</p>
        )}
        {/* QA v3: PUB-007 - wybrany MVP był zapisany, ale strona meczu go nie pokazywała. */}
        {mvp && <p className="mt-2 text-center text-sm font-medium">MVP: {mvp}</p>}
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
