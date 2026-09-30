"use client"

import { useEffect, useState } from "react"

import { cn } from "@/lib/utils"
import type { MatchClock as MatchClockData, MatchStatus } from "@/types/matches"

export type ClockData = MatchClockData & { receivedAt: number }

/** Sekundy gry: wartość z serwera + czas, który upłynął lokalnie od odpowiedzi (tylko gdy mecz trwa). */
export function useElapsedSeconds(clock: ClockData | undefined) {
  const [now, setNow] = useState(() => Date.now())
  const running = clock?.status === "Live"

  useEffect(() => {
    if (!running) return
    const timer = setInterval(() => setNow(Date.now()), 1000)
    return () => clearInterval(timer)
  }, [running])

  if (!clock) return 0
  return running
    ? clock.elapsedSeconds + Math.max(0, Math.floor((now - clock.receivedAt) / 1000))
    : clock.elapsedSeconds
}

export const formatClock = (seconds: number) =>
  `${String(Math.floor(seconds / 60)).padStart(2, "0")}:${String(seconds % 60).padStart(2, "0")}`

/** Minuta meczu do formularza zdarzenia: 0:30 gry to 1. minuta. */
export const currentMinute = (seconds: number) => Math.min(300, Math.floor(seconds / 60) + 1)

export function MatchClock({ seconds, status }: { seconds: number; status: MatchStatus }) {
  return (
    <span
      role="timer"
      aria-label={`Czas meczu ${formatClock(seconds)}`}
      className={cn(
        "font-mono text-4xl font-semibold tabular-nums",
        status === "Live" && "text-live",
        status === "Paused" && "text-live/70 animate-pulse",
        (status === "Scheduled" || status === "Finished") && "text-muted-foreground",
      )}
    >
      {formatClock(seconds)}
    </span>
  )
}
