"use client"

import { ArrowLeft, CalendarClock, Shield } from "lucide-react"
import Link from "next/link"
import { type ReactNode, useMemo } from "react"

import { EmptyState } from "@/components/layout/empty-state"
import { Skeleton } from "@/components/ui/skeleton"
import {
  bracketMatchStages,
  plannedBracketMatches,
} from "@/features/matches/planned-bracket-matches"
import { CupPathCard, TeamStats } from "@/features/teams/components/team-stats"
import { TeamLogo } from "@/features/teams/components/team-logo"
import { cupPath, teamRecord } from "@/features/teams/team-record"
import { PLAYER_POSITION_LABELS, type PlayerPosition } from "@/types/teams"

import {
  usePublicBracket,
  usePublicFullRanking,
  usePublicSchedule,
  usePublicSquad,
  usePublicStandings,
} from "../api"
import { usePublicContext } from "../public-context"
import { PublicMatchCard } from "./public-match-card"

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

const UPCOMING = new Set(["Scheduled", "Ready", "Postponed"])

/** Strona drużyny w portalu: bilans, mecze z wynikami, droga w pucharze i skład. */
export function TeamView({ teamId }: { teamId: string }) {
  const { slug, tournament, teams, lookups } = usePublicContext()
  const s = tournament.settings
  const team = teams.find((t) => t.tournamentTeamId === teamId)
  const hasGroups = tournament.format !== "Knockout"
  const hasKnockout = tournament.format === "Knockout" || tournament.format === "GroupAndKnockout"

  const schedule = usePublicSchedule(slug, s.showSchedule || s.showLiveMatches)
  const standings = usePublicStandings(slug, team?.groupId ?? "", hasGroups && s.showStandings)
  const bracket = usePublicBracket(slug, hasKnockout && team ? team.categoryId : undefined)
  const squad = usePublicSquad(slug, teamId, !!team && s.showPlayers)
  const scorers = usePublicFullRanking(slug, "top-scorers", !!team && s.showPlayers && s.showStats)
  const assists = usePublicFullRanking(slug, "top-assists", !!team && s.showPlayers && s.showStats)

  const matches = useMemo(() => schedule.data ?? [], [schedule.data])
  const record = useMemo(() => teamRecord(matches, teamId), [matches, teamId])
  const cup = useMemo(() => {
    if (!bracket.data || !team) return null
    const planned = plannedBracketMatches(bracket.data, team.categoryId, (id) =>
      lookups.teamName(id),
    )
    return cupPath(matches, teamId, bracketMatchStages(bracket.data), planned)
  }, [bracket.data, matches, team, teamId, lookups])

  if (!team) {
    return (
      <EmptyState
        icon={Shield}
        title="Nie ma takiej drużyny"
        description="Drużyna mogła zostać wycofana z turnieju."
        action={
          <Link href={`/t/${slug}/teams`} className="text-sm font-medium underline">
            Wszystkie drużyny
          </Link>
        }
      />
    )
  }

  const row = standings.data?.find((r) => r.tournamentTeamId === teamId)
  const groupName = lookups.groupName(team.groupId)
  const standing =
    hasGroups && s.showStandings && row && groupName
      ? { position: row.position, points: row.points, groupName }
      : null
  const category = tournament.categories.length > 1 ? lookups.categoryName(team.categoryId) : null
  const links = {
    teamName: (id: string) => lookups.teamName(id),
    teamHref: (id: string) => (id === teamId ? undefined : `/t/${slug}/teams/${id}`),
    matchHref: (m: { id: string }) => `/t/${slug}/matches/${m.id}`,
  }

  // Bez „Meczów na żywo” trwający mecz nie ma osobnej sekcji (jak na stronie głównej portalu).
  const live = s.showLiveMatches ? record.live : []
  const upcoming = record.matches.filter((m) => UPCOMING.has(m.status))
  const played = record.matches
    .filter((m) => m.status === "Finished")
    .sort((a, b) => b.scheduledAt.localeCompare(a.scheduledAt))

  const goals = new Map((scorers.data ?? []).map((e) => [e.playerId, e.count]))
  const assistsBy = new Map((assists.data ?? []).map((e) => [e.playerId, e.count]))

  return (
    <div className="space-y-8">
      <div className="space-y-3">
        <Link
          href={`/t/${slug}/teams`}
          className="text-muted-foreground hover:text-foreground inline-flex items-center gap-1 text-sm"
        >
          <ArrowLeft className="size-4" />
          Drużyny
        </Link>
        <div className="flex items-center gap-4">
          <TeamLogo name={team.name} logoUrl={team.logoUrl} size={56} />
          <div className="min-w-0">
            <h1 className="truncate text-2xl font-semibold">{team.name}</h1>
            <p className="text-muted-foreground text-sm">
              {[category, groupName].filter(Boolean).join(" · ") || tournament.name}
            </p>
          </div>
        </div>
      </div>

      {schedule.isPending && (s.showSchedule || s.showLiveMatches) ? (
        <Skeleton className="h-24 w-full rounded-xl" />
      ) : (
        <TeamStats record={record} standing={standing} />
      )}

      {cup && (
        <CupPathCard cup={cup} teamId={teamId} links={links} showScores={s.showLiveMatches} />
      )}

      {live.length > 0 && (
        <Section title="Na żywo">
          <MatchGrid matches={live} />
        </Section>
      )}
      {s.showSchedule && upcoming.length > 0 && (
        <Section title="Nadchodzące mecze">
          <MatchGrid matches={upcoming} />
        </Section>
      )}
      {s.showSchedule && played.length > 0 && (
        <Section title="Wyniki">
          <MatchGrid matches={played} />
        </Section>
      )}
      {s.showSchedule && !schedule.isPending && record.matches.length === 0 && (
        <EmptyState
          icon={CalendarClock}
          title="Drużyna nie ma jeszcze meczów"
          description="Mecze pojawią się, gdy organizator ułoży terminarz."
        />
      )}

      {s.showPlayers && (
        <Section title="Skład">
          {squad.isPending ? (
            <Skeleton className="h-32 w-full rounded-xl" />
          ) : !squad.data?.length ? (
            <p className="text-muted-foreground text-sm">Organizator nie dodał jeszcze składu.</p>
          ) : (
            <div className="bg-card rounded-xl border">
              <div className="text-muted-foreground grid grid-cols-[2.5rem_1fr_3rem_3rem] gap-2 border-b px-4 py-2 text-xs font-medium">
                <span>Nr</span>
                <span>Zawodnik</span>
                {s.showStats && (
                  <>
                    <span className="text-center" title="Gole">
                      G
                    </span>
                    <span className="text-center" title="Asysty">
                      A
                    </span>
                  </>
                )}
              </div>
              <ol className="divide-y">
                {squad.data.map((p) => (
                  <li
                    key={p.tournamentPlayerId}
                    className="grid grid-cols-[2.5rem_1fr_3rem_3rem] items-center gap-2 px-4 py-2 text-sm"
                    data-testid="squad-player"
                  >
                    <span className="text-muted-foreground font-mono tabular-nums">
                      {p.shirtNumber ?? "–"}
                    </span>
                    <span className="min-w-0">
                      <span className="block truncate font-medium">
                        {p.firstName} {p.lastName}
                      </span>
                      {p.position && p.position !== "Unknown" && (
                        <span className="text-muted-foreground block text-xs">
                          {PLAYER_POSITION_LABELS[p.position as PlayerPosition] ?? p.position}
                        </span>
                      )}
                    </span>
                    {s.showStats && (
                      <>
                        <span className="text-center font-mono tabular-nums">
                          {goals.get(p.playerId) ?? 0}
                        </span>
                        <span className="text-center font-mono tabular-nums">
                          {assistsBy.get(p.playerId) ?? 0}
                        </span>
                      </>
                    )}
                  </li>
                ))}
              </ol>
            </div>
          )}
        </Section>
      )}
    </div>
  )
}

function MatchGrid({ matches }: { matches: Parameters<typeof PublicMatchCard>[0]["match"][] }) {
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
