"use client"

import { useQueries } from "@tanstack/react-query"
import { ArrowRight, Timer } from "lucide-react"
import Link from "next/link"
import { usePathname, useRouter, useSearchParams } from "next/navigation"

import { EmptyState } from "@/components/layout/empty-state"
import { PageHeader } from "@/components/layout/page-header"
import { Button } from "@/components/ui/button"
import { Skeleton } from "@/components/ui/skeleton"
import { matchKeys } from "@/features/matches/api"
import { OpenMatchCard, RefereeMatchCard } from "@/features/matches/components/referee-match-card"
import { useAssignedMatches, useOpenMatches } from "@/features/matches/live-api"
import { useTournamentMatchesRealtime } from "@/features/matches/use-tournament-matches-realtime"
import { teamKeys } from "@/features/teams/api"
import { tournamentKeys } from "@/features/tournaments/api"
import { useApi } from "@/hooks/use-api"
import { cn } from "@/lib/utils"
import { useActiveTournamentStore } from "@/stores/active-tournament-store"
import { useAuthStore } from "@/stores/auth-store"
import type { Match, Pitch } from "@/types/matches"
import type { TournamentTeam } from "@/types/teams"
import type { Tournament } from "@/types/tournaments"

const LIVE = new Set(["Live", "Paused"])
const UPCOMING = new Set(["Scheduled", "Ready", "Postponed"])

/**
 * Lista meczów sędziego zwraca same identyfikatory drużyn i boisk — nazwy dociągamy z turniejów,
 * w których sędziuje (sędzia jest ich członkiem, więc ma do nich dostęp). Te same klucze co reszta
 * aplikacji, więc dane dzielą cache z widokami turnieju.
 */
function useTournamentLookups(tournamentIds: string[]) {
  const { request, isAuthenticated } = useApi()
  const teams = useQueries({
    queries: tournamentIds.map((id) => ({
      queryKey: teamKeys.tournamentTeams(id),
      queryFn: () => request<TournamentTeam[]>(`/api/tournaments/${id}/teams`),
      enabled: isAuthenticated,
    })),
  })
  const pitches = useQueries({
    queries: tournamentIds.map((id) => ({
      queryKey: matchKeys.pitches(id),
      queryFn: () => request<Pitch[]>(`/api/tournaments/${id}/pitches`),
      enabled: isAuthenticated,
    })),
  })
  const tournaments = useQueries({
    queries: tournamentIds.map((id) => ({
      queryKey: tournamentKeys.detail(id),
      queryFn: () => request<Tournament>(`/api/tournaments/${id}`),
      enabled: isAuthenticated,
    })),
  })

  const teamNames = new Map(teams.flatMap((q) => q.data ?? []).map((t) => [t.id, t.teamName]))
  const pitchNames = new Map(pitches.flatMap((q) => q.data ?? []).map((p) => [p.id, p.name]))
  const tournamentById = new Map(
    tournaments.flatMap((q) => (q.data ? [[q.data.id.toLowerCase(), q.data] as const] : [])),
  )
  return { teamNames, pitchNames, tournamentById }
}

function Section({
  title,
  description,
  matches,
  lookups,
  open = false,
}: {
  title: string
  description?: string
  matches: Match[]
  lookups: ReturnType<typeof useTournamentLookups>
  /** Mecze bez sędziego — zamiast wejścia do panelu mają przycisk przejęcia. */
  open?: boolean
}) {
  if (matches.length === 0) return null
  const MatchCard = open ? OpenMatchCard : RefereeMatchCard
  return (
    <section className="space-y-3">
      <div className="space-y-1">
        <h2 className="text-muted-foreground text-xs font-medium tracking-wide uppercase">
          {title}
        </h2>
        {description && <p className="text-muted-foreground text-sm">{description}</p>}
      </div>
      <ul className="grid gap-3 xl:grid-cols-2">
        {matches.map((match) => (
          <li key={match.id}>
            <MatchCard
              match={match}
              homeName={lookups.teamNames.get(match.homeTournamentTeamId) ?? "Drużyna"}
              awayName={lookups.teamNames.get(match.awayTournamentTeamId) ?? "Drużyna"}
              pitchName={match.pitchId ? (lookups.pitchNames.get(match.pitchId) ?? "Boisko") : null}
              // Lista jest już podzielona na turnieje, nazwa na karcie byłaby powtórzeniem.
              tournamentName={null}
            />
          </li>
        ))}
      </ul>
    </section>
  )
}

type TournamentTab = {
  id: string
  name: string
  live: number
  upcoming: number
  /** Najbliższy nadchodzący mecz (własny albo wolny), do ustawienia kolejności zakładek. */
  next: string | null
}

/**
 * Zakładki turniejów, w których user sędziuje. Mecze z różnych turniejów na jednej liście łatwo
 * pomylić, więc naraz widać tylko jeden turniej.
 */
function TournamentTabs({
  tabs,
  selectedId,
  onSelect,
}: {
  tabs: TournamentTab[]
  selectedId: string
  onSelect: (id: string) => void
}) {
  return (
    <nav aria-label="Turnieje" className="-mx-4 overflow-x-auto px-4 lg:mx-0 lg:px-0">
      <ul className="flex min-w-max gap-2">
        {tabs.map((tab) => {
          const active = tab.id === selectedId
          const count = tab.live + tab.upcoming
          return (
            <li key={tab.id}>
              <button
                type="button"
                aria-pressed={active}
                onClick={() => onSelect(tab.id)}
                className={cn(
                  "flex items-center gap-2 rounded-full border px-3 py-1.5 text-sm font-medium transition-colors",
                  active
                    ? "border-primary bg-primary/15 text-primary"
                    : "text-muted-foreground hover:text-foreground hover:border-foreground/30",
                )}
              >
                {tab.live > 0 && (
                  <span className="bg-live size-2 animate-pulse rounded-full" aria-hidden />
                )}
                {tab.name}
                {count > 0 && (
                  <span
                    className="bg-secondary text-foreground rounded-full px-1.5 text-xs tabular-nums"
                    aria-label={`${count} do poprowadzenia`}
                  >
                    {count}
                  </span>
                )}
              </button>
            </li>
          )
        })}
      </ul>
    </nav>
  )
}

const LAST = "￿"

export function RefereeMatchesView() {
  const router = useRouter()
  const pathname = usePathname()
  const searchParams = useSearchParams()
  const activeTournament = useActiveTournamentStore((state) => state.tournament)
  const setActiveTournament = useActiveTournamentStore((state) => state.setTournament)
  const roles = useAuthStore((state) => state.roleAssignments)

  const matches = useAssignedMatches()
  const openMatches = useOpenMatches()
  const all = matches.data ?? []
  const open = openMatches.data ?? []
  // Także turnieje, w których sędzia nie ma jeszcze żadnego meczu, inaczej by "znikały".
  const refereeTournamentIds = (roles ?? []).flatMap((r) =>
    r.role === "Referee" && r.scopeType === "Tournament" && r.scopeId
      ? [r.scopeId.toLowerCase()]
      : [],
  )
  const tournamentIds = [
    ...new Set([
      ...[...all, ...open].map((m) => m.tournamentId.toLowerCase()),
      ...refereeTournamentIds,
    ]),
  ]
  const lookups = useTournamentLookups(tournamentIds)
  // Inny sędzia wziął albo oddał mecz: listy odświeżają się same.
  useTournamentMatchesRealtime(tournamentIds)
  const inTournament = (id: string) => (m: Match) => m.tournamentId.toLowerCase() === id

  const tabs: TournamentTab[] = tournamentIds
    .map((id) => {
      const own = all.filter(inTournament(id))
      const free = open.filter(inTournament(id))
      const both = [...own, ...free]
      return {
        id,
        name: lookups.tournamentById.get(id)?.name ?? "Turniej",
        live: both.filter((m) => LIVE.has(m.status)).length,
        upcoming: own.filter((m) => UPCOMING.has(m.status)).length,
        next:
          both
            .filter((m) => UPCOMING.has(m.status))
            .map((m) => m.scheduledAt)
            .sort()[0] ?? null,
      }
    })
    // Najpierw turnieje z trwającym meczem, potem wg najbliższego meczu, na końcu bez meczów.
    .sort(
      (a, b) =>
        Number(b.live > 0) - Number(a.live > 0) ||
        (a.next ?? LAST).localeCompare(b.next ?? LAST) ||
        a.name.localeCompare(b.name, "pl"),
    )

  const fromUrl = searchParams.get("tournament")?.toLowerCase()
  const fromStore = activeTournament?.id.toLowerCase()
  const selectedId =
    tabs.find((t) => t.id === fromUrl)?.id ??
    // Trwający mecz jest ważniejszy niż ostatnio oglądany turniej.
    tabs.find((t) => t.live > 0)?.id ??
    tabs.find((t) => t.id === fromStore)?.id ??
    tabs[0]?.id
  const selected = selectedId ? lookups.tournamentById.get(selectedId) : undefined

  const select = (id: string) => {
    router.replace(`${pathname}?tournament=${id}`, { scroll: false })
    const tournament = lookups.tournamentById.get(id)
    if (tournament)
      setActiveTournament({ id: tournament.id, name: tournament.name, format: tournament.format })
  }

  const mine = selectedId ? all.filter(inTournament(selectedId)) : []
  const free = selectedId ? open.filter(inTournament(selectedId)) : []
  const live = mine.filter((m) => LIVE.has(m.status))
  const upcoming = mine.filter((m) => UPCOMING.has(m.status))
  const past = mine
    .filter((m) => !LIVE.has(m.status) && !UPCOMING.has(m.status))
    .sort((a, b) => b.scheduledAt.localeCompare(a.scheduledAt))
  // Trwający mecz bez sędziego (ktoś go oddał w trakcie) jest pilny, więc idzie na samą górę.
  const openLive = free.filter((m) => LIVE.has(m.status))
  const openUpcoming = free.filter((m) => !LIVE.has(m.status))

  return (
    <div className="space-y-6">
      <PageHeader
        title="Moje mecze"
        description="Mecze, które prowadzisz jako sędzia, i wolne mecze do wzięcia."
      />
      {matches.isPending || openMatches.isPending ? (
        <div className="grid gap-3 xl:grid-cols-2">
          {[0, 1, 2].map((i) => (
            <Skeleton key={i} className="h-28 rounded-xl" />
          ))}
        </div>
      ) : !selectedId ? (
        <EmptyState
          icon={Timer}
          title="Nie masz meczów do sędziowania"
          description="Dołącz do turnieju kodem sędziego. Zobaczysz tu mecze, które możesz wziąć, i te przydzielone przez organizatora."
          action={
            <Button asChild variant="outline">
              <Link href="/join">Dołącz kodem</Link>
            </Button>
          }
        />
      ) : (
        <>
          {tabs.length > 1 && (
            <TournamentTabs tabs={tabs} selectedId={selectedId} onSelect={select} />
          )}
          <div className="flex flex-wrap items-center justify-between gap-2 border-b pb-3">
            <h2 className="text-lg font-semibold">{selected?.name ?? "Turniej"}</h2>
            <Button asChild variant="ghost" size="sm">
              <Link href={`/tournaments/${selected?.id ?? selectedId}/schedule`}>
                Terminarz turnieju
                <ArrowRight />
              </Link>
            </Button>
          </div>
          {mine.length === 0 && free.length === 0 ? (
            <EmptyState
              icon={Timer}
              title="W tym turnieju nie masz jeszcze meczów"
              description="Gdy organizator przydzieli Ci mecz albo pojawi się wolny, zobaczysz go tutaj."
            />
          ) : (
            <>
              <Section
                title="Trwają bez sędziego"
                description="Te mecze już się toczą i nikt ich nie prowadzi."
                matches={openLive}
                lookups={lookups}
                open
              />
              <Section title="Teraz" matches={live} lookups={lookups} />
              <Section title="Nadchodzące" matches={upcoming} lookups={lookups} />
              <Section
                title="Wolne mecze"
                description="Nie mają jeszcze sędziego. Weź ten, który poprowadzisz."
                matches={openUpcoming}
                lookups={lookups}
                open
              />
              <Section title="Zakończone" matches={past} lookups={lookups} />
            </>
          )}
        </>
      )}
    </div>
  )
}
