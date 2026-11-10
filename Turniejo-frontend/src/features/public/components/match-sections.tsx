"use client"

import { CalendarClock, CalendarX, Hourglass, MapPin } from "lucide-react"
import Link from "next/link"
import { type ReactNode, useEffect, useState } from "react"

import { EmptyState } from "@/components/layout/empty-state"
import { Skeleton } from "@/components/ui/skeleton"
import {
  type BracketStage,
  bracketStages,
  type PlannedBracketMatch,
  plannedBracketMatches,
} from "@/features/matches/planned-bracket-matches"
import { usePublicSecondStage } from "@/features/second-stage/api"
import { plannedSecondStageMatches } from "@/features/second-stage/planned"
import { formatDateTime, formatDayLong, plural, toZonedDateInput } from "@/lib/format"
import type { PublicMatch } from "@/types/public"
import { hasKnockoutStage } from "@/types/tournaments"

import { usePublicBrackets, usePublicSchedule } from "../api"
import { readFavoriteTeam, writeFavoriteTeam } from "../favorite-team"
import { usePublicContext } from "../public-context"
import {
  applyMatchFilters,
  hasMatchFilters,
  type MatchFilters,
  matchFilterOptions,
  matchFiltersToSearch,
  NO_MATCH_FILTERS,
} from "../match-filters"
import {
  type ActiveFilter,
  ActiveFilters,
  ALL,
  FilterGrid,
  FilterOption,
  FilterPanel,
  FilterSelect,
} from "./filter-bar"
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

/**
 * Filtry meczów. Drużyna z linku (?team=) ma pierwszeństwo przed zapamiętaną i sama staje się
 * zapamiętaną, żeby przetrwała przejście do innej sekcji portalu. Reszta filtrów żyje w adresie.
 */
function useMatchFilters(initial: MatchFilters) {
  const { slug, teams } = usePublicContext()
  const [filters, setFilters] = useState<MatchFilters>(() => ({
    ...initial,
    teamId: initial.teamId ?? readFavoriteTeam(slug),
  }))
  useEffect(() => {
    if (initial.teamId) writeFavoriteTeam(slug, initial.teamId)
  }, [slug, initial.teamId])

  // Zapamiętana drużyna mogła zniknąć z turnieju (albo organizator ukrył drużyny).
  const teamId =
    filters.teamId && teams.some((t) => t.tournamentTeamId === filters.teamId)
      ? filters.teamId
      : null
  const active = { ...filters, teamId }

  function update(patch: Partial<MatchFilters>) {
    const next = { ...active, ...patch }
    if ("teamId" in patch) writeFavoriteTeam(slug, next.teamId)
    setFilters(next)
    // Adres do udostępnienia („mecze naszej drużyny na boisku 2”), bez przeładowania strony.
    const search = matchFiltersToSearch(next)
    window.history.replaceState(null, "", search ? `?${search}` : window.location.pathname)
  }
  return { filters: active, update }
}

const capitalize = (text: string) => text.charAt(0).toUpperCase() + text.slice(1)

function MatchFilterBar({
  filters,
  update,
  matches,
  count,
}: {
  filters: MatchFilters
  update: (patch: Partial<MatchFilters>) => void
  matches: PublicMatch[]
  count: number
}) {
  const { tournament, teams } = usePublicContext()
  const options = matchFilterOptions(tournament, matches, filters.categoryId)
  const sortedTeams = [...teams].sort((a, b) =>
    a.name.localeCompare(b.name, "pl", { numeric: true }),
  )
  const value = (v: string | null) => v ?? ALL
  const pick = (v: string) => (v === ALL ? null : v)

  const selects = [
    // Bez ShowTeams backend nie zdradza drużyn, więc nie ma czego filtrować.
    sortedTeams.length > 0 && (
      <FilterSelect
        key="team"
        label="Drużyna"
        value={value(filters.teamId)}
        onChange={(v) => update({ teamId: pick(v) })}
        allLabel="Wszystkie drużyny"
      >
        {sortedTeams.map((t) => (
          <FilterOption key={t.tournamentTeamId} value={t.tournamentTeamId}>
            {t.name}
          </FilterOption>
        ))}
      </FilterSelect>
    ),
    options.categories.length > 0 && (
      <FilterSelect
        key="category"
        label="Kategoria"
        value={value(filters.categoryId)}
        onChange={(v) => {
          const categoryId = pick(v)
          const group = tournament.groups.find((g) => g.id === filters.groupId)
          // Grupa z innej kategorii dałaby zawsze pustą listę.
          const otherCategory = !!group && !!categoryId && group.categoryId !== categoryId
          update(otherCategory ? { categoryId, groupId: null } : { categoryId })
        }}
        allLabel="Wszystkie"
      >
        {options.categories.map((c) => (
          <FilterOption key={c.id} value={c.id}>
            {c.name}
          </FilterOption>
        ))}
      </FilterSelect>
    ),
    options.groups.length > 0 && (
      <FilterSelect
        key="group"
        label="Grupa"
        value={value(filters.groupId)}
        onChange={(v) => update({ groupId: pick(v) })}
        allLabel="Wszystkie"
      >
        {options.groups.map((g) => (
          <FilterOption key={g.id} value={g.id}>
            {g.label}
          </FilterOption>
        ))}
      </FilterSelect>
    ),
    options.pitches.length > 0 && (
      <FilterSelect
        key="pitch"
        label="Boisko"
        value={value(filters.pitchId)}
        onChange={(v) => update({ pitchId: pick(v) })}
        allLabel="Wszystkie"
      >
        {options.pitches.map((p) => (
          <FilterOption key={p.id} value={p.id}>
            {p.name}
          </FilterOption>
        ))}
      </FilterSelect>
    ),
    options.days.length > 0 && (
      <FilterSelect
        key="day"
        label="Dzień"
        value={value(filters.day)}
        onChange={(v) => update({ day: pick(v) })}
        allLabel="Wszystkie dni"
      >
        {options.days.map((d) => (
          <FilterOption key={d} value={d}>
            {capitalize(formatDayLong(d))}
          </FilterOption>
        ))}
      </FilterSelect>
    ),
  ].filter(Boolean)
  if (selects.length === 0) return null

  const name = (list: { id: string; name: string }[], id: string | null) =>
    list.find((x) => x.id === id)?.name ?? ""
  const active: ActiveFilter[] = []
  if (filters.teamId)
    active.push({
      key: "team",
      label: sortedTeams.find((t) => t.tournamentTeamId === filters.teamId)?.name ?? "Drużyna",
      clear: () => update({ teamId: null }),
    })
  if (filters.categoryId)
    active.push({
      key: "cat",
      label: name(tournament.categories, filters.categoryId),
      clear: () => update({ categoryId: null }),
    })
  if (filters.groupId)
    active.push({
      key: "group",
      label: options.groups.find((g) => g.id === filters.groupId)?.label ?? "Grupa",
      clear: () => update({ groupId: null }),
    })
  if (filters.pitchId)
    active.push({
      key: "pitch",
      label: name(tournament.pitches, filters.pitchId),
      clear: () => update({ pitchId: null }),
    })
  if (filters.day)
    active.push({
      key: "day",
      label: capitalize(formatDayLong(filters.day)),
      clear: () => update({ day: null }),
    })

  return (
    <div className="space-y-3">
      <FilterPanel label="Filtry meczów">
        <FilterGrid count={selects.length}>{selects}</FilterGrid>
      </FilterPanel>
      {active.length > 0 && (
        <ActiveFilters
          summary={`Znaleziono ${plural(count, ["mecz", "mecze", "meczów"])}`}
          filters={active}
          onClearAll={() => update(NO_MATCH_FILTERS)}
        />
      )}
    </div>
  )
}

function useFilteredSchedule(initialFilters: MatchFilters) {
  const { slug, tournament } = usePublicContext()
  // Bez terminarza kibic dalej widzi mecze na żywo (QA v3: PUB-003c) - backend zwraca wtedy tylko je.
  const schedule = usePublicSchedule(
    slug,
    tournament.settings.showSchedule || tournament.settings.showLiveMatches,
  )
  const { filters: active, update } = useMatchFilters(initialFilters)
  const all = schedule.data ?? []
  const matches = applyMatchFilters(all, active)
  const filters = (
    <MatchFilterBar filters={active} update={update} matches={all} count={matches.length} />
  )
  return { schedule, matches, filters, filtered: hasMatchFilters(active) }
}

/** Strona główna portalu: teraz grane, najbliższe i ostatnie wyniki. */
export function MatchesOverview({ initialFilters }: { initialFilters: MatchFilters }) {
  const { slug, tournament } = usePublicContext()
  const { schedule, matches, filters, filtered } = useFilteredSchedule(initialFilters)

  const { showSchedule, showLiveMatches } = tournament.settings
  const cancelledTournament = tournament.status === "Cancelled"
  if (!showSchedule && !showLiveMatches) {
    return <EmptyState icon={CalendarClock} title="Organizator nie udostępnił terminarza" />
  }
  if (schedule.isPending) return <Skeleton className="h-64 w-full rounded-xl" />

  // Wyłączone „Mecze na żywo”: trwające mecze nie mają osobnej sekcji (QA v3: PUB-003f).
  const live = showLiveMatches ? matches.filter(isLive) : []
  if (!showSchedule) {
    return live.length > 0 ? (
      <Section title="Na żywo">
        <Grid matches={live} />
      </Section>
    ) : (
      <EmptyState icon={CalendarClock} title="Teraz nic się nie toczy" />
    )
  }
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
        // Anulowany turniej nie dostanie terminarza - bez tego strona obiecywała go „wkrótce” (QA v3: PUB-012).
        <EmptyState
          icon={CalendarClock}
          title={
            filtered
              ? "Brak meczów dla wybranych filtrów"
              : cancelledTournament
                ? "Turniej został odwołany"
                : "Brak meczów"
          }
          description={
            filtered
              ? undefined
              : cancelledTournament
                ? "Organizator odwołał ten turniej. Mecze się nie odbędą."
                : "Terminarz pojawi się wkrótce."
          }
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
 * W formacie „Dwie fazy grupowe” w ten sam sposób idą grupy drugiej fazy.
 */
function useKnockoutStages(): KnockoutStage[] {
  const { slug, tournament, lookups } = usePublicContext()
  const { settings, format, status, categories } = tournament
  // Przed wygenerowaniem terminarza nie ma jeszcze obowiązujących terminów.
  const scheduled =
    status !== "Draft" && status !== "RegistrationOpen" && status !== "RegistrationClosed"
  const enabled = settings.showSchedule && settings.showBracket && hasKnockoutStage(format) && scheduled
  const brackets = usePublicBrackets(
    slug,
    categories.map((c) => c.id),
    enabled,
  )
  const secondStage = usePublicSecondStage(
    slug,
    settings.showSchedule && format === "TwoGroupStages" && scheduled,
  )

  if (format === "TwoGroupStages") {
    const data = secondStage.data
    if (!data || !scheduled) return []
    const planned = plannedSecondStageMatches(data, tournament.groups, (id) => lookups.teamName(id))
    return tournament.groups
      .filter((g) => g.stage === 2)
      .sort((a, b) => a.order - b.order)
      .map((group, i) => ({
        roundId: group.id,
        order: i,
        title:
          categories.length > 1
            ? `${categories.find((c) => c.id === group.categoryId)?.name ?? ""} · ${group.name}`
            : group.name,
        matchIds: data.fixtures
          .filter((f) => f.groupId === group.id && f.matchId)
          .map((f) => f.matchId!),
        planned: planned.filter((p) => p.secondStageGroupId === group.id),
      }))
  }
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
        {planned.stage} · {planned.secondStageGroupId ? "czeka na koniec fazy 1" : "czeka na wynik"}
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

/**
 * Pełny terminarz: mecze grupowe po dniach, faza pucharowa po rundach (ćwierćfinały, półfinały…),
 * a grupy drugiej fazy osobno, każda ze swoimi meczami i zapowiedziami.
 */
export function FullSchedule({ initialFilters }: { initialFilters: MatchFilters }) {
  const { schedule, matches, filters, filtered } = useFilteredSchedule(initialFilters)
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
