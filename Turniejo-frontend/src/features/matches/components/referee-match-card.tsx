"use client"

import { ChevronRight, Clock, Hand, MapPin } from "lucide-react"
import Link from "next/link"
import { useRouter } from "next/navigation"
import { toast } from "sonner"

import { Button } from "@/components/ui/button"
import { TeamLogo } from "@/features/teams/components/team-logo"
import { Card, CardContent } from "@/components/ui/card"
import { prefetchHandlers, usePrefetch } from "@/hooks/use-prefetch"
import { formatDateTime } from "@/lib/format"
import { cn } from "@/lib/utils"
import type { Match } from "@/types/matches"

import { liveKeys, useMatchRefereeSelf } from "../live-api"
import { MatchStatusBadge } from "./match-status-badge"
import { ReleaseMatchButton } from "./release-match-button"

type RefereeMatchCardProps = {
  match: Match
  homeName: string
  awayName: string
  homeLogo?: string | null
  awayLogo?: string | null
  pitchName: string | null
  tournamentName: string | null
}

const isLive = (match: Match) => match.status === "Live" || match.status === "Paused"

function MatchInfo({
  match,
  homeName,
  awayName,
  homeLogo = null,
  awayLogo = null,
  pitchName,
  tournamentName,
}: RefereeMatchCardProps) {
  const showScore = isLive(match) || match.status === "Finished"
  return (
    <div className="min-w-0 flex-1 space-y-2">
      <div className="flex flex-wrap items-center gap-2 text-xs">
        <MatchStatusBadge status={match.status} awaitingDecision={match.awaitingDecision} />
        {tournamentName && <span className="text-muted-foreground truncate">{tournamentName}</span>}
      </div>
      <p className="flex items-center gap-2 font-medium">
        <TeamLogo name={homeName} logoUrl={homeLogo} size={20} />
        <span className="min-w-0 truncate">{homeName}</span>
        {showScore ? (
          <span className="shrink-0 font-mono tabular-nums">
            {match.homeScore}:{match.awayScore}
          </span>
        ) : (
          <span className="text-muted-foreground shrink-0 text-sm">vs</span>
        )}
        <TeamLogo name={awayName} logoUrl={awayLogo} size={20} />
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
  )
}

/**
 * Mecz sędziego: status, drużyny, boisko, godzina — karta prowadzi do panelu live. Dopóki mecz się nie
 * skończył, sędzia może go oddać z powrotem do puli.
 */
export function RefereeMatchCard(props: RefereeMatchCardProps) {
  const { match, homeName, awayName } = props
  const live = isLive(match)
  const releasable = match.status !== "Finished" && match.status !== "Cancelled"
  const prefetch = usePrefetch()
  const warmUp = () =>
    prefetch(
      { queryKey: liveKeys.location(match.id), path: `/api/matches/${match.id}/location` },
      {
        queryKey: liveKeys.panel(match.id),
        path: `/api/tournaments/${match.tournamentId}/matches/${match.id}/live-panel`,
      },
    )
  return (
    <Card
      className={cn(
        "hover:border-primary/50 gap-1 py-0 transition-colors",
        live && "border-live/60",
      )}
    >
      <Link
        href={`/matches/${match.id}/live`}
        {...prefetchHandlers(warmUp)}
        className={cn(
          "focus-visible:ring-ring block rounded-xl pt-4 outline-none focus-visible:ring-2",
          !releasable && "pb-4",
        )}
      >
        <CardContent className="flex items-center gap-4 px-4">
          <MatchInfo {...props} />
          <ChevronRight className="text-muted-foreground size-5 shrink-0" />
        </CardContent>
      </Link>
      {releasable && (
        <div className="flex justify-end px-2 pb-2">
          <ReleaseMatchButton
            tournamentId={match.tournamentId}
            matchId={match.id}
            title={`${homeName} – ${awayName}`}
            inProgress={live}
          />
        </div>
      )}
    </Card>
  )
}

/**
 * Mecz bez sędziego: bierze go pierwszy sędzia, który kliknie. Sędzia zostaje na liście, żeby móc
 * wziąć kilka meczów z rzędu. Tylko trwający mecz otwiera od razu, bo ktoś musi go przejąć teraz.
 */
export function OpenMatchCard(props: RefereeMatchCardProps) {
  const { match, homeName, awayName } = props
  const router = useRouter()
  const claim = useMatchRefereeSelf("claim")
  const panelHref = `/matches/${match.id}/live`
  return (
    <Card className={cn("py-4", isLive(match) && "border-live/60")}>
      <CardContent className="flex flex-col gap-3 px-4 sm:flex-row sm:items-center sm:gap-4">
        <MatchInfo {...props} />
        <Button
          size="sm"
          className="shrink-0"
          disabled={claim.isPending}
          aria-label={`Sędziuję ten mecz: ${homeName} – ${awayName}`}
          onClick={() =>
            claim.mutate(
              { tournamentId: match.tournamentId, matchId: match.id },
              {
                onSuccess: () => {
                  if (isLive(match)) {
                    toast.success("Mecz jest Twój")
                    router.push(panelHref)
                    return
                  }
                  toast.success(`Mecz jest Twój: ${homeName} – ${awayName}`, {
                    description: "Znajdziesz go w sekcji „Nadchodzące”.",
                    action: { label: "Otwórz panel", onClick: () => router.push(panelHref) },
                  })
                },
              },
            )
          }
        >
          <Hand />
          {claim.isPending ? "Zapisywanie…" : "Sędziuję ten mecz"}
        </Button>
      </CardContent>
    </Card>
  )
}
