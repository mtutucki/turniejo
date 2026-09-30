"use client"

import { CalendarClock } from "lucide-react"
import { type ReactNode, useState } from "react"

import { EmptyState } from "@/components/layout/empty-state"
import { Skeleton } from "@/components/ui/skeleton"
import { formatDayLong, toZonedDateInput } from "@/lib/format"
import type { PublicMatch } from "@/types/public"

import { usePublicSchedule } from "../api"
import { usePublicContext } from "../public-context"
import {
  ALL_MATCHES,
  type MatchFilter,
  MatchFilterPills,
  matchesFilter,
} from "./match-filter-pills"
import { PublicMatchCard } from "./public-match-card"

const isLive = (m: PublicMatch) => m.status === "Live" || m.status === "Paused"
const UPCOMING = new Set(["Scheduled", "Ready", "Postponed"])

function Section({ title, children }: { title: string; children: ReactNode }) {
  return (
    <section className="space-y-3">
      <h2 className="text-muted-foreground text-xs font-semibold tracking-wide uppercase">
        {title}
      </h2>
      {children}
    </section>
  )
}

function Grid({ matches }: { matches: PublicMatch[] }) {
  return (
    <ul className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
      {matches.map((match) => (
        <li key={match.id}>
          <PublicMatchCard match={match} />
        </li>
      ))}
    </ul>
  )
}

function useFilteredSchedule() {
  const { slug, tournament } = usePublicContext()
  const schedule = usePublicSchedule(slug, tournament.settings.showSchedule)
  const [filter, setFilter] = useState<MatchFilter>(ALL_MATCHES)
  const matches = (schedule.data ?? []).filter((m) => matchesFilter(m, filter))
  return { schedule, filter, setFilter, matches }
}

/** Strona główna portalu: teraz grane, najbliższe i ostatnie wyniki. */
export function MatchesOverview() {
  const { tournament } = usePublicContext()
  const { schedule, filter, setFilter, matches } = useFilteredSchedule()

  if (!tournament.settings.showSchedule) {
    return <EmptyState icon={CalendarClock} title="Organizator nie udostępnił terminarza" />
  }
  if (schedule.isPending) return <Skeleton className="h-64 w-full rounded-xl" />

  const live = matches.filter(isLive)
  const upcoming = matches.filter((m) => UPCOMING.has(m.status)).slice(0, 6)
  const results = matches
    .filter((m) => m.status === "Finished")
    .sort((a, b) => b.scheduledAt.localeCompare(a.scheduledAt))
    .slice(0, 6)

  return (
    <div className="space-y-6">
      <MatchFilterPills value={filter} onChange={setFilter} />
      {matches.length === 0 ? (
        <EmptyState
          icon={CalendarClock}
          title="Brak meczów"
          description="Terminarz pojawi się wkrótce."
        />
      ) : (
        <>
          {live.length > 0 && (
            <Section title="Na żywo">
              <Grid matches={live} />
            </Section>
          )}
          {upcoming.length > 0 && (
            <Section title="Najbliższe mecze">
              <Grid matches={upcoming} />
            </Section>
          )}
          {results.length > 0 && (
            <Section title="Ostatnie wyniki">
              <Grid matches={results} />
            </Section>
          )}
        </>
      )}
    </div>
  )
}

/** Pełny terminarz pogrupowany po dniach. */
export function FullSchedule() {
  const { schedule, filter, setFilter, matches } = useFilteredSchedule()
  if (schedule.isPending) return <Skeleton className="h-64 w-full rounded-xl" />

  const days = new Map<string, PublicMatch[]>()
  for (const match of matches) {
    const key = toZonedDateInput(match.scheduledAt)
    days.set(key, [...(days.get(key) ?? []), match])
  }

  return (
    <div className="space-y-6">
      <MatchFilterPills value={filter} onChange={setFilter} />
      {matches.length === 0 ? (
        <EmptyState icon={CalendarClock} title="Brak meczów" />
      ) : (
        [...days.values()].map((dayMatches) => (
          <Section key={dayMatches[0]!.id} title={formatDayLong(dayMatches[0]!.scheduledAt)}>
            <Grid matches={dayMatches} />
          </Section>
        ))
      )}
    </div>
  )
}
