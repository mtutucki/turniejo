"use client"

import { Minus, Plus } from "lucide-react"
import { AnimatePresence, motion } from "motion/react"

import { Button } from "@/components/ui/button"
import type { MatchStatus, MatchTeam } from "@/types/matches"

import { MatchClock } from "./match-clock"
import { MatchStatusBadge } from "./match-status-badge"

/** Liczba, która "wskakuje" przy zmianie — gol od razu rzuca się w oczy (także z SignalR). */
export function AnimatedScore({ value, className }: { value: number; className?: string }) {
  return (
    <span className="relative inline-flex overflow-hidden">
      <AnimatePresence mode="popLayout" initial={false}>
        <motion.span
          key={value}
          className={className}
          initial={{ y: "-60%", opacity: 0, scale: 1.3 }}
          animate={{ y: 0, opacity: 1, scale: 1 }}
          exit={{ y: "60%", opacity: 0 }}
          transition={{ type: "spring", stiffness: 420, damping: 28 }}
        >
          {value}
        </motion.span>
      </AnimatePresence>
    </span>
  )
}

function TeamScore({
  team,
  score,
  canEdit,
  onPlus,
  onMinus,
  minusDisabled,
}: {
  team: MatchTeam
  score: number
  canEdit: boolean
  onPlus: () => void
  onMinus: () => void
  minusDisabled: boolean
}) {
  const label = team.shortName || team.name
  return (
    <div className="flex min-w-0 flex-col items-center gap-3">
      <p className="w-full truncate text-center text-sm font-medium" title={team.name}>
        {team.name}
      </p>
      <p
        className="font-mono text-7xl leading-none font-bold tabular-nums"
        aria-label={`${team.name}: ${score}`}
        data-testid="score"
      >
        <AnimatedScore value={score} />
      </p>
      {canEdit && (
        <div className="grid w-full grid-cols-2 gap-2">
          <Button
            variant="outline"
            className="h-14"
            onClick={onMinus}
            disabled={minusDisabled}
            aria-label={`Cofnij ostatniego gola: ${label}`}
          >
            <Minus className="size-5" />
          </Button>
          <Button
            variant="outline"
            className="h-14"
            onClick={onPlus}
            aria-label={`Szybki gol: ${label}`}
          >
            <Plus className="size-5" />
          </Button>
        </div>
      )}
    </div>
  )
}

export type LiveScoreBoardProps = {
  status: MatchStatus
  homeTeam: MatchTeam
  awayTeam: MatchTeam
  homeScore: number
  awayScore: number
  homePenaltyScore?: number | null
  awayPenaltyScore?: number | null
  /** Sekundy gry (zegar meczu). */
  seconds: number
  /** Sędzia/organizator: przyciski +/− przy wyniku. */
  canEdit: boolean
  onGoal: (team: MatchTeam) => void
  onUndo: (team: MatchTeam) => void
  /** Czy drużyna ma gola do cofnięcia (i nic się właśnie nie cofa). */
  canUndo: (team: MatchTeam) => boolean
}

/** Tablica wyniku panelu sędziego: status, zegar, wynik (z karnymi) i szybkie +/−. */
export function LiveScoreBoard({
  status,
  homeTeam,
  awayTeam,
  homeScore,
  awayScore,
  homePenaltyScore,
  awayPenaltyScore,
  seconds,
  canEdit,
  onGoal,
  onUndo,
  canUndo,
}: LiveScoreBoardProps) {
  return (
    <>
      <section aria-label="Wynik" className="bg-card rounded-2xl border p-4">
        <div className="mb-4 flex flex-col items-center gap-2">
          <MatchStatusBadge status={status} />
          <MatchClock seconds={seconds} status={status} />
        </div>
        <div className="grid grid-cols-[1fr_auto_1fr] items-start gap-3">
          <TeamScore
            team={homeTeam}
            score={homeScore}
            canEdit={canEdit}
            onPlus={() => onGoal(homeTeam)}
            onMinus={() => onUndo(homeTeam)}
            minusDisabled={!canUndo(homeTeam)}
          />
          <span className="text-muted-foreground pt-9 font-mono text-4xl">:</span>
          <TeamScore
            team={awayTeam}
            score={awayScore}
            canEdit={canEdit}
            onPlus={() => onGoal(awayTeam)}
            onMinus={() => onUndo(awayTeam)}
            minusDisabled={!canUndo(awayTeam)}
          />
        </div>
      </section>

      {homePenaltyScore != null && awayPenaltyScore != null && (
        <p className="text-muted-foreground -mt-3 text-center text-sm" data-testid="penalties">
          karne{" "}
          <span className="font-mono tabular-nums">
            {homePenaltyScore}:{awayPenaltyScore}
          </span>
        </p>
      )}
    </>
  )
}
