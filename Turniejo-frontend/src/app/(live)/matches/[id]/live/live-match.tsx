"use client"

import {
  ArrowLeft,
  Flag,
  Pause,
  Play,
  Plus,
  ShieldAlert,
  Square,
  Star,
  Wifi,
  WifiOff,
} from "lucide-react"
import { useQuery } from "@tanstack/react-query"
import Link from "next/link"
import { useParams, useRouter } from "next/navigation"
import { useEffect, useRef, useState } from "react"

import { EmptyState } from "@/components/layout/empty-state"
import { Button } from "@/components/ui/button"
import { Skeleton } from "@/components/ui/skeleton"
import { ThemeToggle } from "@/features/theme/theme-toggle"
import { useTournamentSettings } from "@/features/tournaments/api"
import { EventTimeline, playerLabel } from "@/features/matches/components/event-timeline"
import { FinishMatchDialog } from "@/features/matches/components/finish-match-dialog"
import { currentMinute, useElapsedSeconds } from "@/features/matches/components/match-clock"
import {
  AddCardDialog,
  AddGoalDialog,
  SelectMvpDialog,
} from "@/features/matches/components/match-event-dialogs"
import { LiveScoreBoard } from "@/features/matches/components/live-score-board"
import { ReleaseMatchButton } from "@/features/matches/components/release-match-button"
import {
  scoringTeamId,
  useAddGoal,
  useLivePanel,
  useMatchClock,
  useMatchLifecycle,
  useMatchLocation,
  useRevertEvent,
} from "@/features/matches/live-api"
import { type RealtimeState, useMatchRealtime } from "@/features/matches/use-match-realtime"
import { useApi } from "@/hooks/use-api"
import { myRolesQueryKey } from "@/hooks/use-my-roles"
import { isApiError } from "@/lib/api/errors"
import { formatTime } from "@/lib/format"
import { cn } from "@/lib/utils"
import { type MatchLivePanel, type MatchTeam, isGoal } from "@/types/matches"
import { selectRoles } from "@/stores/auth-store"
import { type RoleAssignment, UserRole } from "@/types/roles"

const refereeMatchesHref = (tournamentId?: string) =>
  tournamentId ? `/referee/matches?tournament=${tournamentId}` : "/referee/matches"

/**
 * Sędzia wraca do swojej listy meczów. Organizator prowadzący mecz bez przypisania jako sędzia nie ma
 * tam nic — wraca do terminarza turnieju, z którego otworzył panel.
 */
function BackLink({
  scheduleTournamentId,
  tournamentId,
}: {
  scheduleTournamentId?: string
  /** Lista meczów sędziego otwiera się na turnieju, z którego jest ten mecz. */
  tournamentId?: string
}) {
  return (
    <Button asChild variant="ghost" size="sm" className="-ml-2">
      <Link
        href={
          scheduleTournamentId
            ? `/tournaments/${scheduleTournamentId}/schedule`
            : refereeMatchesHref(tournamentId)
        }
      >
        <ArrowLeft />
        {scheduleTournamentId ? "Terminarz" : "Moje mecze"}
      </Link>
    </Button>
  )
}

function ConnectionBadge({ state }: { state: RealtimeState }) {
  const online = state === "connected"
  return (
    <span
      role="status"
      className={cn(
        "flex items-center gap-1.5 text-xs",
        online ? "text-primary" : "text-muted-foreground",
        state === "disconnected" && "text-destructive",
      )}
    >
      {online ? <Wifi className="size-3.5" /> : <WifiOff className="size-3.5" />}
      {state === "connected"
        ? "Na żywo"
        : state === "reconnecting"
          ? "Łączenie ponownie…"
          : state === "connecting"
            ? "Łączenie…"
            : "Brak połączenia"}
    </span>
  )
}

/** Czy wolno dodawać/cofać zdarzenia — to samo co Match.EnsureEditable w domenie. */
function useCanEdit(panel: MatchLivePanel | undefined) {
  const lockAt = panel?.editLockedAt ? new Date(panel.editLockedAt).getTime() : null
  const [now, setNow] = useState(() => Date.now())
  useEffect(() => {
    if (!lockAt) return
    const timer = setInterval(() => setNow(Date.now()), 15_000)
    return () => clearInterval(timer)
  }, [lockAt])

  if (!panel) return false
  if (panel.status === "Live" || panel.status === "Paused") return true
  return panel.status === "Finished" && lockAt != null && now < lockAt
}

/**
 * MVP meczu. Sędzia wybiera je jak inne zdarzenia (w trakcie i w oknie poprawek); organizator także
 * później — backend (SelectMatchMvp) przepuszcza go po zamknięciu okna edycji.
 */
function MvpSection({
  tournamentId,
  panel,
  canSelect,
}: {
  tournamentId: string
  panel: MatchLivePanel
  canSelect: boolean
}) {
  const current = panel.events.filter((e) => e.type === "MvpSelected").at(-1)
  const team =
    current?.tournamentTeamId === panel.homeTeam.tournamentTeamId ? panel.homeTeam : panel.awayTeam
  const player = playerLabel(panel, current?.playerId ?? null)

  return (
    <section
      aria-label="MVP meczu"
      className="bg-card flex items-center gap-3 rounded-2xl border px-4 py-3"
    >
      <Star className="size-5 shrink-0 fill-yellow-400 text-yellow-500" aria-hidden />
      <div className="min-w-0 flex-1">
        <p className="text-muted-foreground text-xs font-medium tracking-wide uppercase">
          MVP meczu
        </p>
        <p className="truncate text-sm font-medium">
          {current && player ? (
            <>
              {player} <span className="text-muted-foreground font-normal">· {team.name}</span>
            </>
          ) : (
            <span className="text-muted-foreground font-normal">Nie wybrano</span>
          )}
        </p>
      </div>
      {canSelect && (
        <SelectMvpDialog
          tournamentId={tournamentId}
          panel={panel}
          current={current}
          trigger={
            <Button variant="outline" className="h-10">
              {current ? "Zmień" : "Wybierz"}
            </Button>
          }
        />
      )}
    </section>
  )
}

function LivePanel({
  tournamentId,
  panel,
  isOrganizer,
}: {
  tournamentId: string
  panel: MatchLivePanel
  isOrganizer: boolean
}) {
  const matchId = panel.matchId
  const clock = useMatchClock(tournamentId, matchId)
  const seconds = useElapsedSeconds(clock.data)
  const minute = currentMinute(seconds)
  const settings = useTournamentSettings(tournamentId)
  const lifecycle = useMatchLifecycle(tournamentId, matchId)
  const addGoal = useAddGoal(tournamentId, matchId)
  const revert = useRevertEvent(tournamentId, matchId)
  const canEdit = useCanEdit(panel)
  const { status } = panel

  function quickGoal(team: MatchTeam) {
    addGoal.mutate({
      tournamentTeamId: team.tournamentTeamId,
      playerId: null,
      assistPlayerId: null,
      goalType: "Goal",
      minute,
    })
  }

  // "−" cofa ostatniego gola zaliczonego tej drużynie (także samobója przeciwnika).
  const lastGoalFor = (team: MatchTeam) =>
    panel.events
      .filter(
        (e) =>
          isGoal(e.type) &&
          !e.id.startsWith("temp-") &&
          scoringTeamId(panel, e) === team.tournamentTeamId,
      )
      .at(-1)

  function undoGoal(team: MatchTeam) {
    const goal = lastGoalFor(team)
    if (!goal) return
    revert.mutate(goal)
  }

  // Bez toastów potwierdzeń: zmiana i tak jest widoczna na tablicy, a toast na górze ekranu telefonu
  // zasłaniał nagłówek (powrót do listy). Toasty zostają dla błędów i ostrzeżeń (druga żółta).
  // Blokada do zmiany statusu meczu: isPending dochodzi do przycisku z opóźnieniem, a po sukcesie,
  // zanim panel pobierze nowy status, START był znowu aktywny - drugie kliknięcie dawało 409 (QA v3: MNZ-005).
  const lockedFor = useRef<string | null>(null)
  const [lockedStatus, setLockedStatus] = useState<string | null>(null)
  const locked = lifecycle.isPending || lockedStatus === status
  function run(action: "start" | "pause" | "resume") {
    if (lockedFor.current === status) return
    lockedFor.current = status
    setLockedStatus(status)
    lifecycle.mutate(action, {
      onError: () => {
        lockedFor.current = null
        setLockedStatus(null)
      },
    })
  }

  return (
    <div className="space-y-5">
      <LiveScoreBoard
        status={status}
        homeTeam={panel.homeTeam}
        awayTeam={panel.awayTeam}
        homeScore={panel.homeScore}
        awayScore={panel.awayScore}
        homePenaltyScore={panel.homePenaltyScore}
        awayPenaltyScore={panel.awayPenaltyScore}
        resultType={panel.resultType}
        seconds={seconds}
        awaitingDecision={panel.awaitingDecision}
        canEdit={canEdit}
        onGoal={quickGoal}
        onUndo={undoGoal}
        canUndo={(team) => !!lastGoalFor(team) && !revert.isPending}
      />

      {/* Sterowanie meczem */}
      {status === "Scheduled" && (
        <div className="space-y-3">
          <Button
            className="h-16 w-full text-lg"
            onClick={() => run("start")}
            disabled={locked}
          >
            <Play className="size-6" />
            START
          </Button>
          {/* Mecz rozegrany bez prowadzenia w aplikacji: sam wynik, bez zegara. */}
          <FinishMatchDialog
            mode="record"
            tournamentId={tournamentId}
            panel={panel}
            trigger={
              <Button variant="outline" className="h-12 w-full">
                Wpisz sam wynik (bez zegara)
              </Button>
            }
          />
        </div>
      )}

      {(status === "Live" || status === "Paused") && (
        <div className="grid grid-cols-2 gap-3">
          {status === "Live" ? (
            <Button
              variant="outline"
              className="h-16 text-base"
              onClick={() => run("pause")}
              disabled={locked}
            >
              <Pause className="size-5" />
              PAUZA
            </Button>
          ) : (
            <Button
              className="h-16 text-base"
              onClick={() => run("resume")}
              disabled={locked}
            >
              <Play className="size-5" />
              WZNÓW
            </Button>
          )}
          <FinishMatchDialog
            tournamentId={tournamentId}
            panel={panel}
            editWindowMinutes={settings.data?.refereeEditWindowMinutes}
            trigger={
              <Button
                variant="outline"
                className="border-destructive/50 text-destructive h-16 text-base"
              >
                <Square className="size-5" />
                KONIEC
              </Button>
            }
          />
        </div>
      )}

      {canEdit && (
        <div className="grid grid-cols-2 gap-3">
          <AddGoalDialog
            tournamentId={tournamentId}
            panel={panel}
            minute={minute}
            trigger={
              <Button className="h-16 text-base">
                <Plus className="size-5" />
                GOL
              </Button>
            }
          />
          <AddCardDialog
            tournamentId={tournamentId}
            panel={panel}
            minute={minute}
            trigger={
              <Button variant="secondary" className="h-16 text-base">
                <span className="h-5 w-4 rounded-xs bg-yellow-400" aria-hidden />
                KARTKA
              </Button>
            }
          />
        </div>
      )}

      {status === "Finished" && (
        <div className="bg-muted/40 space-y-3 rounded-lg border p-3 text-sm">
          <p className="flex items-start gap-2">
            <Flag className="text-primary mt-0.5 size-4 shrink-0" />
            {canEdit && panel.editLockedAt
              ? `Mecz zakończony. Poprawki możliwe do ${formatTime(panel.editLockedAt)}.`
              : isOrganizer
                ? "Mecz zakończony. Jako organizator nadal możesz zmienić rozstrzygnięcie."
                : "Mecz zakończony - wynik jest zablokowany."}
          </p>
          {/* Sędzia w czasie na poprawki, organizator zawsze (backend: ChangeFinishedMatchResult). */}
          {(canEdit || isOrganizer) && (
            <FinishMatchDialog
              mode="change"
              tournamentId={tournamentId}
              panel={panel}
              trigger={
                <Button variant="outline" className="h-12 w-full">
                  Zmień rozstrzygnięcie (karne, walkower)
                </Button>
              }
            />
          )}
        </div>
      )}

      {status !== "Scheduled" && status !== "Cancelled" && (
        <MvpSection
          tournamentId={tournamentId}
          panel={panel}
          canSelect={canEdit || (status === "Finished" && isOrganizer)}
        />
      )}

      <section aria-label="Zdarzenia" className="bg-card rounded-2xl border px-4 py-2">
        <h2 className="text-muted-foreground py-2 text-xs font-medium tracking-wide uppercase">
          Zdarzenia
        </h2>
        <EventTimeline
          tournamentId={tournamentId}
          panel={panel}
          canEdit={canEdit}
          canEditScorers={canEdit || (status === "Finished" && isOrganizer)}
          onRevert={(event) => revert.mutate(event)}
        />
      </section>
    </div>
  )
}

export function LiveMatchView() {
  const { id: matchId } = useParams<{ id: string }>()
  const router = useRouter()
  const { request, isAuthenticated } = useApi()
  const location = useMatchLocation(matchId)
  const tournamentId = location.data?.tournamentId
  // Role pobierane przy każdym wejściu do panelu: sędzia przypisany przez organizatora, kiedy miał już
  // otwartą aplikację, inaczej widziałby "To nie jest Twój mecz" do przeładowania strony.
  const roles = useQuery({
    queryKey: myRolesQueryKey,
    queryFn: () => request<RoleAssignment[]>("/api/me/roles"),
    enabled: isAuthenticated,
    refetchOnMount: "always",
  })
  const isOrganizer = selectRoles(roles.data ?? null, "Tournament", tournamentId).includes(
    UserRole.Organizer,
  )
  const isMatchReferee = selectRoles(roles.data ?? null, "Match", matchId).includes(
    UserRole.Referee,
  )
  const canOperate = isOrganizer || isMatchReferee
  const rolesLoading = roles.isPending || (roles.isFetching && !canOperate)

  const realtime = useMatchRealtime(matchId, canOperate && !!tournamentId)
  const panel = useLivePanel(
    canOperate ? tournamentId : undefined,
    matchId,
    realtime === "connected" ? false : 5000,
  )

  const header = (
    <header className="flex items-center justify-between gap-3">
      <BackLink
        scheduleTournamentId={isOrganizer && !isMatchReferee ? tournamentId : undefined}
        tournamentId={tournamentId}
      />
      <div className="flex items-center gap-1">
        {isMatchReferee &&
          tournamentId &&
          panel.data &&
          panel.data.status !== "Finished" &&
          panel.data.status !== "Cancelled" && (
            <ReleaseMatchButton
              tournamentId={tournamentId}
              matchId={matchId}
              title={`${panel.data.homeTeam.name} – ${panel.data.awayTeam.name}`}
              inProgress={panel.data.status === "Live" || panel.data.status === "Paused"}
              onReleased={() => router.push(refereeMatchesHref(tournamentId))}
            />
          )}
        {canOperate && <ConnectionBadge state={realtime} />}
        {/* Na boisku w słońcu jasny motyw bywa czytelniejszy, więc przełącznik jest pod ręką. */}
        <ThemeToggle className="hover:bg-accent" />
      </div>
    </header>
  )

  let content
  if (location.isError) {
    const status = isApiError(location.error) ? location.error.status : 0
    content = (
      <EmptyState
        icon={ShieldAlert}
        title={status === 403 ? "Brak dostępu do tego meczu" : "Nie znaleziono meczu"}
      />
    )
  } else if (location.isPending || rolesLoading) {
    content = <Skeleton className="h-96 w-full rounded-2xl" />
  } else if (!canOperate) {
    // Panel prowadzi tylko sędzia tego meczu albo organizator (to samo co komendy meczu → 403).
    content = (
      <EmptyState
        icon={ShieldAlert}
        title="To nie jest Twój mecz"
        description="Panel meczu prowadzi sędzia, który go wziął albo dostał od organizatora. Wolne mecze znajdziesz w „Moje mecze”."
      />
    )
  } else if (panel.isPending) {
    content = <Skeleton className="h-96 w-full rounded-2xl" />
  } else if (panel.data) {
    content = (
      <LivePanel tournamentId={tournamentId!} panel={panel.data} isOrganizer={isOrganizer} />
    )
  } else {
    content = <EmptyState icon={ShieldAlert} title="Nie udało się pobrać meczu" />
  }

  return (
    <div className="mx-auto w-full max-w-md space-y-4 p-4 pb-10">
      {header}
      {content}
    </div>
  )
}
