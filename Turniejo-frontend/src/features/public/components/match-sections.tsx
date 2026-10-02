"use client"

import { CalendarClock, CalendarX, Hourglass, MapPin } from "lucide-react"
import Link from "next/link"
import { type ReactNode, useEffect, useState } from "react"

import { EmptyState } from "@/components/layout/empty-state"
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select"
import { Skeleton } from "@/components/ui/skeleton"
import {
  type BracketStage,
  bracketStages,
  type PlannedBracketMatch,
  plannedBracketMatches,
} from "@/features/matches/planned-bracket-matches"
import { formatDateTime, formatDayLong, plural, toZonedDateInput } from "@/lib/format"
import type { PublicMatch } from "@/types/public"

import { usePublicBrackets, usePublicSchedule } from "../api"
import { readFavoriteTeam, writeFavoriteTeam } from "../favorite-team"
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

const ALL_TEAMS = "all"

/**
 * Filtr "mecze mojej drużyny". Wybór z linku (?team=) ma pierwszeństwo przed zapamiętanym — i sam
 * staje się zapamiętanym, żeby przetrwał przejście do innej sekcji portalu.
 */
function useTeamFilter(initialTeamId: string | undefined) {
  const { slug, teams } = usePublicContext()
  const [teamId, setTeamId] = useState(() => initialTeamId ?? readFavoriteTeam(slug))
  useEffect(() => {
    if (initialTeamId) writeFavoriteTeam(slug, initialTeamId)
  }, [slug, initialTeamId])

  // Zapamiętana drużyna mogła zniknąć z turnieju (albo organizator ukrył drużyny).
  const active = teamId && teams.some((t) => t.tournamentTeamId === teamId) ? teamId : null

  function select(next: string | null) {
    setTeamId(next)
    writeFavoriteTeam(slug, next)
    // Adres do udostępnienia ("mecze naszej drużyny") — bez przeładowania strony.
    window.history.replaceState(
      null,
      "",
      next ? `?team=${encodeURIComponent(next)}` : window.location.pathname,
    )
  }
  return { teamId: active, select }
}

function TeamSelect({
  value,
  onChange,
}: {
  value: string | null
  onChange: (teamId: string | null) => void
}) {
  const { teams } = usePublicContext()
  // Bez ShowTeams backend nie zdradza drużyn — nie ma czego filtrować.
  if (teams.length === 0) return null
  const sorted = [...teams].sort((a, b) => a.name.localeCompare(b.name, "pl", { numeric: true }))

  return (
    <Select
      value={value ?? ALL_TEAMS}
      onValueChange={(next) => onChange(next === ALL_TEAMS ? null : next)}
    >
      <SelectTrigger aria-label="Drużyna" className="w-full sm:w-72">
        <SelectValue />
      </SelectTrigger>
      <SelectContent>
        <SelectItem value={ALL_TEAMS}>Wszystkie drużyny</SelectItem>
        {sorted.map((team) => (
          <SelectItem key={team.tournamentTeamId} value={team.tournamentTeamId}>
            {team.name}
          </SelectItem>
        ))}
      </SelectContent>
    </Select>
  )
}

function useFilteredSchedule(initialTeamId: string | undefined) {
  const { slug, tournament } = usePublicContext()
  const schedule = usePublicSchedule(slug, tournament.settings.showSchedule)
  const [filter, setFilter] = useState<MatchFilter>(ALL_MATCHES)
  const team = useTeamFilter(initialTeamId)
  const matches = (schedule.data ?? []).filter(
    (m) =>
      matchesFilter(m, filter) &&
      (!team.teamId ||
        m.homeTournamentTeamId === team.teamId ||
        m.awayTournamentTeamId === team.teamId),
  )
  const filters = (
    <div className="space-y-3">
      <TeamSelect value={team.teamId} onChange={team.select} />
      <MatchFilterPills value={filter} onChange={setFilter} />
    </div>
  )
  return { schedule, matches, filters, filtered: !!team.teamId || filter.kind !== "all" }
}

/** Strona główna portalu: teraz grane, najbliższe i ostatnie wyniki. */
export function MatchesOverview({ initialTeamId }: { initialTeamId?: string }) {
  const { slug, tournament } = usePublicContext()
  const { schedule, matches, filters, filtered } = useFilteredSchedule(initialTeamId)

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
  const cancelled = matches.filter((m) => m.status === "Cancelled").length

  return (
    <div className="space-y-6">
      {filters}
      {matches.length === 0 ? (
        <EmptyState
          icon={CalendarClock}
          title={filtered ? "Brak meczów dla wybranych filtrów" : "Brak meczów"}
          description={filtered ? undefined : "Terminarz pojawi się wkrótce."}
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
          {/* Odwołane mecze nie trafiają do żadnej sekcji — bez tej notki znikałyby bez śladu. */}
          {cancelled > 0 && (
            <p className="text-muted-foreground flex flex-wrap items-center gap-x-2 gap-y-1 text-sm">
              <CalendarX className="size-4 shrink-0" aria-hidden />
              {plural(cancelled, [
                "mecz został odwołany",
                "mecze zostały odwołane",
                "meczów zostało odwołanych",
              ])}
              .
              <Link
                href={`/t/${slug}/schedule`}
                className="text-primary font-medium underline-offset-4 hover:underline"
              >
                Zobacz w terminarzu
              </Link>
            </p>
          )}
        </>
      )}
    </div>
  )
}

type KnockoutStage = BracketStage & { planned: PlannedBracketMatch[] }

/**
 * Rundy drabinek wszystkich kategorii, w kolejności rozgrywania. Każda niesie też zapowiedzi:
 * mecze, których termin jest już zaplanowany, ale pary wyłonią dopiero wcześniejsze mecze.
 */
function useKnockoutStages(): KnockoutStage[] {
  const { slug, tournament, lookups } = usePublicContext()
  const { settings, format, status, categories } = tournament
  // Puchar przed wygenerowaniem terminarza nie ma jeszcze obowiązujących terminów.
  const enabled =
    settings.showSchedule &&
    settings.showBracket &&
    (format === "Knockout" || format === "GroupAndKnockout") &&
    status !== "Draft" &&
    status !== "RegistrationOpen" &&
    status !== "RegistrationClosed"
  const brackets = usePublicBrackets(
    slug,
    categories.map((c) => c.id),
    enabled,
  )
  if (!enabled) return []

  return brackets.flatMap((bracket, i) => {
    if (!bracket.data) return []
    const category = categories[i]!
    const planned = plannedBracketMatches(bracket.data, category.id, (id) => lookups.teamName(id))
    return bracketStages(bracket.data).map((stage) => ({
      ...stage,
      // Przy kilku kategoriach same "Półfinały" byłyby niejednoznaczne.
      title: categories.length > 1 ? `${category.name} · ${stage.title}` : stage.title,
      planned: planned.filter((p) => p.roundId === stage.roundId),
    }))
  })
}

function PlannedCard({ planned }: { planned: PlannedBracketMatch }) {
  const { lookups } = usePublicContext()
  const pitch = lookups.pitchName(planned.pitchId)
  return (
    <div
      className="bg-card h-full space-y-2 rounded-xl border border-dashed p-4 text-sm"
      data-testid="planned-bracket-match"
    >
      <p className="text-muted-foreground flex items-center gap-1 text-xs font-medium">
        <Hourglass className="size-3" aria-hidden />
        {planned.stage} · czeka na wynik
      </p>
      {[planned.home, planned.away].map((side, i) => (
        <p key={i} className={side.known ? "font-medium" : "text-muted-foreground italic"}>
          {side.label}
        </p>
      ))}
      <p className="text-muted-foreground flex flex-wrap items-center gap-x-3 text-xs">
        <span>{formatDateTime(planned.scheduledAt)}</span>
        {pitch && (
          <span className="flex items-center gap-1">
            <MapPin className="size-3" aria-hidden />
            {pitch}
          </span>
        )}
      </p>
    </div>
  )
}

/** Jedna runda pucharu: rozegrane i ustalone mecze razem z zapowiedziami, po kolei według terminu. */
function StageSection({
  stage,
  matches,
  planned,
}: {
  stage: KnockoutStage
  matches: PublicMatch[]
  planned: PlannedBracketMatch[]
}) {
  const items = [
    ...matches.map((match) => ({
      key: match.id,
      at: match.scheduledAt,
      card: <PublicMatchCard match={match} />,
    })),
    ...planned.map((p) => ({
      key: p.nodeId,
      at: p.scheduledAt,
      card: <PlannedCard planned={p} />,
    })),
  ].sort((a, b) => a.at.localeCompare(b.at))
  // Karta meczu pokazuje samą godzinę — dzień trafia do nagłówka rundy.
  const days = [...new Set(items.map((item) => formatDayLong(item.at)))]

  return (
    <Section title={`${stage.title} · ${days.join(" / ")}`}>
      <ul className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
        {items.map((item) => (
          <li key={item.key}>{item.card}</li>
        ))}
      </ul>
    </Section>
  )
}

/** Pełny terminarz: mecze grupowe po dniach, faza pucharowa po rundach (ćwierćfinały, półfinały…). */
export function FullSchedule({ initialTeamId }: { initialTeamId?: string }) {
  const { schedule, matches, filters, filtered } = useFilteredSchedule(initialTeamId)
  const stages = useKnockoutStages()
  if (schedule.isPending) return <Skeleton className="h-64 w-full rounded-xl" />

  const inStage = new Set(stages.flatMap((stage) => stage.matchIds))
  const days = new Map<string, PublicMatch[]>()
  for (const match of matches) {
    if (inStage.has(match.id)) continue
    const key = toZonedDateInput(match.scheduledAt)
    days.set(key, [...(days.get(key) ?? []), match])
  }

  const stageSections = stages
    .map((stage) => ({
      stage,
      matches: matches.filter((m) => stage.matchIds.includes(m.id)),
      // Przy filtrze (drużyna, grupa) zapowiedzi bez znanych par tylko by myliły.
      planned: filtered ? [] : stage.planned,
    }))
    .filter((s) => s.matches.length > 0 || s.planned.length > 0)

  return (
    <div className="space-y-6">
      {filters}
      {matches.length === 0 && (
        <EmptyState
          icon={CalendarClock}
          title={filtered ? "Brak meczów dla wybranych filtrów" : "Brak meczów"}
        />
      )}
      {[...days.values()].map((dayMatches) => (
        <Section key={dayMatches[0]!.id} title={formatDayLong(dayMatches[0]!.scheduledAt)}>
          <Grid matches={dayMatches} />
        </Section>
      ))}
      {stageSections.map((s) => (
        <StageSection key={s.stage.roundId} {...s} />
      ))}
    </div>
  )
}
