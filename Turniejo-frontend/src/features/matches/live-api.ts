"use client"

import { type QueryClient, useMutation, useQuery, useQueryClient } from "@tanstack/react-query"

import { useApi } from "@/hooks/use-api"
import { myRolesQueryKey } from "@/hooks/use-my-roles"
import {
  type AddCardInput,
  type AddGoalInput,
  type CardType,
  type Match,
  type MatchClock,
  type MatchEvent,
  MatchEventType,
  type MatchLivePanel,
  type MatchLocation,
  MatchResultType,
  isGoal,
} from "@/types/matches"

import { matchKeys } from "./api"

export const liveKeys = {
  assigned: ["referee", "matches"] as const,
  open: ["referee", "matches", "open"] as const,
  location: (matchId: string) => ["matches", matchId, "location"] as const,
  panel: (matchId: string) => ["matches", matchId, "live-panel"] as const,
  clock: (matchId: string) => ["matches", matchId, "clock"] as const,
}

const matchPath = (tournamentId: string, matchId: string) =>
  `/api/tournaments/${tournamentId}/matches/${matchId}`

// --- Zapytania -----------------------------------------------------------------------------

/** Mecze, do których zalogowany user jest przypisany jako sędzia (GetAssignedMatchesQuery). */
export function useAssignedMatches() {
  const { request, isAuthenticated } = useApi()
  return useQuery({
    queryKey: liveKeys.assigned,
    queryFn: () => request<Match[]>("/api/referee/matches"),
    enabled: isAuthenticated,
  })
}

/** Mecze bez sędziego w turniejach, w których user jest sędzią — może wziąć dowolny z nich. */
export function useOpenMatches() {
  const { request, isAuthenticated } = useApi()
  return useQuery({
    queryKey: liveKeys.open,
    queryFn: () => request<Match[]>("/api/referee/matches/open"),
    enabled: isAuthenticated,
  })
}

/** Adres /matches/[id]/live zna tylko id meczu — turniej bierzemy z lokalizacji meczu. */
export function useMatchLocation(matchId: string) {
  const { request, isAuthenticated } = useApi()
  return useQuery({
    queryKey: liveKeys.location(matchId),
    queryFn: () => request<MatchLocation>(`/api/matches/${matchId}/location`),
    enabled: isAuthenticated,
    retry: false,
  })
}

/** pollMs: odpytywanie, gdy SignalR nie działa — bez tego przy zerwanym połączeniu panel by zamarł. */
export function useLivePanel(
  tournamentId: string | undefined,
  matchId: string,
  pollMs: number | false = false,
) {
  const { request, isAuthenticated } = useApi()
  return useQuery({
    queryKey: liveKeys.panel(matchId),
    queryFn: () => request<MatchLivePanel>(`${matchPath(tournamentId!, matchId)}/live-panel`),
    enabled: isAuthenticated && !!tournamentId,
    refetchInterval: pollMs,
  })
}

/**
 * Zegar liczy serwer (GetElapsedSeconds) — lokalnie tylko dokładamy upływ czasu od odpowiedzi,
 * więc źle ustawiony zegar telefonu sędziego nie przesuwa czasu meczu.
 */
export function useMatchClock(tournamentId: string | undefined, matchId: string) {
  const { request, isAuthenticated } = useApi()
  return useQuery({
    queryKey: liveKeys.clock(matchId),
    queryFn: async () => ({
      ...(await request<MatchClock>(`${matchPath(tournamentId!, matchId)}/clock`)),
      receivedAt: Date.now(),
    }),
    enabled: isAuthenticated && !!tournamentId,
  })
}

// --- Pomocnicze do optimistic updates ------------------------------------------------------

function otherTeam(panel: MatchLivePanel, tournamentTeamId: string | null) {
  return tournamentTeamId === panel.homeTeam.tournamentTeamId
    ? panel.awayTeam.tournamentTeamId
    : panel.homeTeam.tournamentTeamId
}

/** Drużyna, której zaliczono gola — samobój liczy się przeciwnikowi (jak RecalculateMatchScore). */
export function scoringTeamId(
  panel: MatchLivePanel,
  event: Pick<MatchEvent, "type" | "tournamentTeamId">,
) {
  return event.type === "OwnGoal"
    ? otherTeam(panel, event.tournamentTeamId)
    : event.tournamentTeamId
}

function withGoal(panel: MatchLivePanel, teamId: string | null, delta: 1 | -1): MatchLivePanel {
  const isHome = teamId === panel.homeTeam.tournamentTeamId
  return {
    ...panel,
    homeScore: Math.max(0, panel.homeScore + (isHome ? delta : 0)),
    awayScore: Math.max(0, panel.awayScore + (isHome ? 0 : delta)),
  }
}

function tempEvent(
  matchId: string,
  fields: Partial<MatchEvent> & Pick<MatchEvent, "type" | "minute">,
): MatchEvent {
  return {
    // Nie crypto.randomUUID — nie istnieje poza HTTPS/localhost (np. telefon sędziego po IP w sieci lokalnej).
    id: `temp-${Date.now()}-${Math.random().toString(36).slice(2)}`,
    matchId,
    tournamentTeamId: null,
    playerId: null,
    relatedPlayerId: null,
    second: null,
    createdByUserId: "",
    occurredAt: new Date().toISOString(),
    ...fields,
  }
}

type Snapshot = { previous?: MatchLivePanel }

function useOptimisticPanel(matchId: string) {
  const queryClient = useQueryClient()
  const key = liveKeys.panel(matchId)
  return {
    queryClient,
    /** Zapisuje stan przed zmianą i nakłada zmianę od razu — wynik rośnie przed odpowiedzią API. */
    async apply(update: (panel: MatchLivePanel) => MatchLivePanel): Promise<Snapshot> {
      await queryClient.cancelQueries({ queryKey: key })
      const previous = queryClient.getQueryData<MatchLivePanel>(key)
      if (previous) queryClient.setQueryData(key, update(previous))
      return { previous }
    },
    rollback(snapshot: Snapshot | undefined) {
      if (snapshot?.previous) queryClient.setQueryData(key, snapshot.previous)
    },
    setScore(homeScore: number, awayScore: number) {
      queryClient.setQueryData<MatchLivePanel>(key, (panel) =>
        panel ? { ...panel, homeScore, awayScore } : panel,
      )
    },
    refresh: () => invalidateLive(queryClient, matchId),
  }
}

export function invalidateLive(queryClient: QueryClient, matchId: string) {
  void queryClient.invalidateQueries({ queryKey: liveKeys.panel(matchId) })
  void queryClient.invalidateQueries({ queryKey: liveKeys.clock(matchId) })
}

/** Kartki zawodników w meczu — te same reguły co SendOffRules w backendzie. */
export function cardState(panel: MatchLivePanel) {
  const yellow = new Set<string>()
  const red = new Set<string>()
  for (const e of panel.events) {
    if (!e.playerId) continue
    if (e.type === "YellowCard") yellow.add(e.playerId)
    if (e.type === "RedCard") red.add(e.playerId)
  }
  return { yellow, red }
}

/** Druga żółta w meczu zapisuje się jako czerwona. */
export function effectiveCardType(
  panel: MatchLivePanel,
  playerId: string,
  requested: CardType,
): CardType {
  return requested === "YellowCard" && cardState(panel).yellow.has(playerId) ? "RedCard" : requested
}

// --- Mutacje -------------------------------------------------------------------------------

type ClockData = MatchClock & { receivedAt: number }

type ScoreResponse = { homeScore: number; awayScore: number }

export function useAddGoal(tournamentId: string, matchId: string) {
  const { request } = useApi()
  const panel = useOptimisticPanel(matchId)
  return useMutation({
    mutationFn: (input: AddGoalInput) =>
      request<ScoreResponse & { matchEventId: string }>(
        `${matchPath(tournamentId, matchId)}/goals`,
        {
          method: "POST",
          body: { ...input, goalType: MatchEventType[input.goalType], second: null },
        },
      ),
    onMutate: (input) =>
      panel.apply((current) => {
        const event = tempEvent(matchId, {
          type: input.goalType,
          tournamentTeamId: input.tournamentTeamId,
          playerId: input.playerId,
          relatedPlayerId: input.assistPlayerId,
          minute: input.minute,
        })
        return {
          ...withGoal(current, scoringTeamId(current, event), 1),
          events: [...current.events, event],
        }
      }),
    onError: (_error, _input, snapshot) => panel.rollback(snapshot),
    onSuccess: ({ homeScore, awayScore }) => panel.setScore(homeScore, awayScore),
    onSettled: panel.refresh,
  })
}

export function useAddCard(tournamentId: string, matchId: string) {
  const { request } = useApi()
  const panel = useOptimisticPanel(matchId)
  return useMutation({
    mutationFn: (input: AddCardInput) =>
      request<{ matchEventId: string; cardType: CardType }>(
        `${matchPath(tournamentId, matchId)}/cards`,
        {
          method: "POST",
          body: { ...input, cardType: MatchEventType[input.cardType], second: null },
        },
      ),
    onMutate: (input) =>
      panel.apply((current) => ({
        ...current,
        events: [
          ...current.events,
          tempEvent(matchId, {
            type: effectiveCardType(current, input.playerId, input.cardType),
            tournamentTeamId: input.tournamentTeamId,
            playerId: input.playerId,
            minute: input.minute,
          }),
        ],
      })),
    onError: (_error, _input, snapshot) => panel.rollback(snapshot),
    onSettled: panel.refresh,
  })
}

/** Cofnięcie pomyłki — backend oznacza zdarzenie jako usunięte (soft-delete) i przelicza wynik. */
export function useRevertEvent(tournamentId: string, matchId: string) {
  const { request } = useApi()
  const panel = useOptimisticPanel(matchId)
  return useMutation({
    mutationFn: (event: MatchEvent) =>
      request<ScoreResponse>(`${matchPath(tournamentId, matchId)}/events/${event.id}`, {
        method: "DELETE",
      }),
    onMutate: (event) =>
      panel.apply((current) => {
        const withoutEvent = { ...current, events: current.events.filter((e) => e.id !== event.id) }
        return isGoal(event.type)
          ? withGoal(withoutEvent, scoringTeamId(current, event), -1)
          : withoutEvent
      }),
    onError: (_error, _event, snapshot) => panel.rollback(snapshot),
    onSuccess: ({ homeScore, awayScore }) => panel.setScore(homeScore, awayScore),
    onSettled: panel.refresh,
  })
}

/** Wybór MVP zastępuje poprzedni (backend soft-deletuje stare MvpSelected). */
export function useSelectMvp(tournamentId: string, matchId: string) {
  const { request } = useApi()
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (input: { tournamentTeamId: string; playerId: string }) =>
      request<{ matchEventId: string }>(`${matchPath(tournamentId, matchId)}/mvp`, {
        method: "POST",
        body: input,
      }),
    onSettled: () => invalidateLive(queryClient, matchId),
  })
}

/**
 * Sędzia bierze wolny mecz ("claim") albo oddaje swój z powrotem do puli ("release"). Zmienia się też
 * rola na meczu, od której zależy dostęp do panelu — stąd odświeżenie ról.
 */
export function useMatchRefereeSelf(action: "claim" | "release") {
  const { request } = useApi()
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ tournamentId, matchId }: { tournamentId: string; matchId: string }) =>
      request(`${matchPath(tournamentId, matchId)}/referee/${action}`, { method: "POST" }),
    // liveKeys.assigned jest prefiksem liveKeys.open — jedno unieważnienie odświeża obie listy.
    // Także po błędzie: "mecz wziął już ktoś inny" znaczy, że lista wolnych meczów jest nieaktualna.
    onSettled: (_data, _error, { tournamentId }) =>
      Promise.all([
        queryClient.invalidateQueries({ queryKey: myRolesQueryKey }),
        queryClient.invalidateQueries({ queryKey: liveKeys.assigned }),
        queryClient.invalidateQueries({ queryKey: matchKeys.schedule(tournamentId) }),
      ]),
  })
}

type LifecycleAction = "start" | "pause" | "resume"

export function useMatchLifecycle(tournamentId: string, matchId: string) {
  const { request } = useApi()
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (action: LifecycleAction) =>
      request(`${matchPath(tournamentId, matchId)}/${action}`, { method: "POST" }),
    // Zegar i status od razu po odpowiedzi — bez czekania na ponowne pobranie (inaczej po START przez
    // chwilę widać 00:00 i "Zaplanowany"). Refetch w onSettled i tak koryguje wartości z serwera.
    onSuccess: (_data, action) => {
      const now = Date.now()
      queryClient.setQueryData<ClockData>(liveKeys.clock(matchId), (clock) => {
        const elapsed = clock
          ? clock.elapsedSeconds +
            (clock.status === "Live" ? Math.floor((now - clock.receivedAt) / 1000) : 0)
          : 0
        return {
          matchId,
          status: action === "pause" ? "Paused" : "Live",
          elapsedSeconds: action === "start" ? 0 : elapsed,
          receivedAt: now,
        }
      })
      queryClient.setQueryData<MatchLivePanel>(liveKeys.panel(matchId), (panel) =>
        panel ? { ...panel, status: action === "pause" ? "Paused" : "Live" } : panel,
      )
    },
    onSettled: () => {
      invalidateLive(queryClient, matchId)
      void queryClient.invalidateQueries({ queryKey: liveKeys.assigned })
      void queryClient.invalidateQueries({ queryKey: matchKeys.schedule(tournamentId) })
    },
  })
}

export type FinishResultKind = "Regular" | "ExtraTime" | "Penalties" | "Walkover"

export function useFinishMatch(tournamentId: string, matchId: string) {
  const { request } = useApi()
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (input: {
      resultType: FinishResultKind
      winnerTournamentTeamId: string | null
      homePenaltyScore?: number | null
      awayPenaltyScore?: number | null
    }) =>
      request(`${matchPath(tournamentId, matchId)}/finish`, {
        method: "POST",
        body: {
          resultType: MatchResultType[input.resultType],
          winnerTournamentTeamId: input.winnerTournamentTeamId,
          homePenaltyScore: input.homePenaltyScore ?? null,
          awayPenaltyScore: input.awayPenaltyScore ?? null,
        },
      }),
    meta: { handlesValidation: true },
    onSettled: () => {
      invalidateLive(queryClient, matchId)
      void queryClient.invalidateQueries({ queryKey: liveKeys.assigned })
      void queryClient.invalidateQueries({ queryKey: matchKeys.schedule(tournamentId) })
    },
  })
}
