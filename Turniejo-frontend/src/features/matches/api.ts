"use client"

import { useMutation, useQueries, useQuery, useQueryClient } from "@tanstack/react-query"

import { tournamentKeys } from "@/features/tournaments/api"
import { useApi } from "@/hooks/use-api"
import type { GenerateScheduleInput, Match, Pitch, PitchInput } from "@/types/matches"
import type { Bracket, BracketSlotRef, RankingEntry, Standing } from "@/types/public"

import type { PlayerRankingKind } from "./components/player-rankings"

export const matchKeys = {
  pitches: (tournamentId: string) => ["tournaments", tournamentId, "pitches"] as const,
  schedule: (tournamentId: string) => ["tournaments", tournamentId, "schedule"] as const,
  bracket: (tournamentId: string, categoryId: string) =>
    ["tournaments", tournamentId, "bracket", categoryId] as const,
  standings: (tournamentId: string, groupId: string) =>
    ["tournaments", tournamentId, "standings", groupId] as const,
  /** Tabele wszystkich grup turnieju (prefiks do unieważnienia). */
  allStandings: (tournamentId: string) => ["tournaments", tournamentId, "standings"] as const,
  ranking: (tournamentId: string, kind: PlayerRankingKind) =>
    ["tournaments", tournamentId, "ranking", kind] as const,
}

const base = (tournamentId: string) => `/api/tournaments/${tournamentId}`

// --- Boiska --------------------------------------------------------------------------------

export function usePitches(tournamentId: string) {
  const { request, isAuthenticated } = useApi()
  return useQuery({
    queryKey: matchKeys.pitches(tournamentId),
    queryFn: () => request<Pitch[]>(`${base(tournamentId)}/pitches`),
    enabled: isAuthenticated,
    select: (pitches) =>
      [...pitches].sort((a, b) => a.name.localeCompare(b.name, "pl", { numeric: true })),
  })
}

export function useSavePitch(tournamentId: string) {
  const { request } = useApi()
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ pitchId, ...input }: PitchInput & { pitchId?: string }) =>
      pitchId
        ? request(`${base(tournamentId)}/pitches/${pitchId}`, { method: "PUT", body: input })
        : // AddPitch nie przyjmuje IsActive — nowe boisko jest zawsze aktywne.
          request(`${base(tournamentId)}/pitches`, {
            method: "POST",
            body: { name: input.name, description: input.description, groupIds: input.groupIds },
          }),
    meta: { handlesValidation: true },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: matchKeys.pitches(tournamentId) }),
  })
}

export function useDeletePitch(tournamentId: string) {
  const { request } = useApi()
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (pitchId: string) =>
      request(`${base(tournamentId)}/pitches/${pitchId}`, { method: "DELETE" }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: matchKeys.pitches(tournamentId) }),
  })
}

// --- Terminarz -----------------------------------------------------------------------------

export function useSchedule(tournamentId: string) {
  const { request, isAuthenticated } = useApi()
  return useQuery({
    queryKey: matchKeys.schedule(tournamentId),
    queryFn: () => request<Match[]>(`${base(tournamentId)}/schedule`),
    enabled: isAuthenticated,
  })
}

export function useGenerateSchedule(tournamentId: string) {
  const { request } = useApi()
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (input: GenerateScheduleInput) =>
      request<{ matchesCreated: number }>(`${base(tournamentId)}/schedule/generate`, {
        method: "POST",
        body: input,
      }),
    meta: { handlesValidation: true },
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: matchKeys.schedule(tournamentId) })
      // Backend przestawia status turnieju na ScheduleGenerated.
      void queryClient.invalidateQueries({ queryKey: tournamentKeys.detail(tournamentId) })
      // Dwie fazy grupowe: generator planuje mecze fazy 2.
      void queryClient.invalidateQueries({ queryKey: ["tournaments", tournamentId, "second-stage"] })
      void queryClient.invalidateQueries({ queryKey: ["tournaments", "list"] })
    },
  })
}

export function useClearSchedule(tournamentId: string) {
  const { request } = useApi()
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: () =>
      request<{ matchesRemoved: number }>(`${base(tournamentId)}/schedule`, { method: "DELETE" }),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: matchKeys.schedule(tournamentId) })
      // Backend cofa etap turnieju do "Zapisy zamknięte".
      void queryClient.invalidateQueries({ queryKey: tournamentKeys.detail(tournamentId) })
      // Znikają też zaplanowane mecze fazy 2.
      void queryClient.invalidateQueries({ queryKey: ["tournaments", tournamentId, "second-stage"] })
      void queryClient.invalidateQueries({ queryKey: tournamentKeys.dashboard(tournamentId) })
      void queryClient.invalidateQueries({ queryKey: ["tournaments", "list"] })
    },
  })
}

function useInvalidateMatches(tournamentId: string) {
  const queryClient = useQueryClient()
  return () => {
    void queryClient.invalidateQueries({ queryKey: matchKeys.schedule(tournamentId) })
    // Zmiana terminu meczu odsuwa planowane mecze kolejnych rund pucharu (terminy na węzłach drabinki).
    void queryClient.invalidateQueries({ queryKey: ["tournaments", tournamentId, "bracket"] })
    void queryClient.invalidateQueries({ queryKey: tournamentKeys.dashboard(tournamentId) })
  }
}

export function useUpdateMatchSchedule(tournamentId: string) {
  const { request } = useApi()
  const invalidate = useInvalidateMatches(tournamentId)
  return useMutation({
    mutationFn: ({
      matchId,
      pitchId,
      scheduledAt,
      shiftFollowing = false,
      shiftOtherPitches = false,
    }: {
      matchId: string
      pitchId: string | null
      scheduledAt: string
      /** Późniejsze, nierozpoczęte mecze na tym samym boisku przesuwają się o tyle samo. */
      shiftFollowing?: boolean
      /** Potwierdzenie, że przesunięcie rusza też mecze na innych boiskach (409 Conflict.OtherPitches). */
      shiftOtherPitches?: boolean
    }) =>
      request(`${base(tournamentId)}/matches/${matchId}/schedule`, {
        method: "PUT",
        body: { pitchId, scheduledAt, shiftFollowing, shiftOtherPitches },
      }),
    // 409 pokazuje okno zmiany terminu: pytanie o inne boiska albo komunikat w formularzu.
    meta: { handlesValidation: true, handlesConflict: true },
    onSuccess: invalidate,
  })
}

export function useAssignReferee(tournamentId: string) {
  const { request } = useApi()
  const invalidate = useInvalidateMatches(tournamentId)
  return useMutation({
    mutationFn: ({ matchId, refereeId }: { matchId: string; refereeId: string | null }) =>
      request(`${base(tournamentId)}/matches/${matchId}/referee`, {
        method: "PATCH",
        body: { refereeId },
      }),
    meta: { handlesValidation: true },
    onSuccess: invalidate,
  })
}

// Mecz kolejnej rundy pucharu, który jeszcze nie powstał: termin i sędzia zapisują się na węźle drabinki.
export function useUpdatePlannedMatchSchedule(tournamentId: string) {
  const { request } = useApi()
  const invalidate = useInvalidateMatches(tournamentId)
  return useMutation({
    mutationFn: ({
      nodeId,
      pitchId,
      scheduledAt,
    }: {
      nodeId: string
      pitchId: string
      scheduledAt: string
    }) =>
      request(`${base(tournamentId)}/bracket-nodes/${nodeId}/schedule`, {
        method: "PUT",
        body: { pitchId, scheduledAt },
      }),
    meta: { handlesValidation: true },
    onSuccess: invalidate,
  })
}

export function useAssignPlannedReferee(tournamentId: string) {
  const { request } = useApi()
  const invalidate = useInvalidateMatches(tournamentId)
  return useMutation({
    mutationFn: ({ nodeId, refereeId }: { nodeId: string; refereeId: string | null }) =>
      request(`${base(tournamentId)}/bracket-nodes/${nodeId}/referee`, {
        method: "PATCH",
        body: { refereeId },
      }),
    meta: { handlesValidation: true },
    onSuccess: invalidate,
  })
}

// --- Drabinka ------------------------------------------------------------------------------

export function useBracket(tournamentId: string, categoryId: string | undefined) {
  const { request, isAuthenticated } = useApi()
  return useQuery({
    queryKey: matchKeys.bracket(tournamentId, categoryId ?? ""),
    queryFn: () =>
      request<Bracket>(
        `${base(tournamentId)}/bracket?categoryId=${encodeURIComponent(categoryId!)}`,
      ),
    enabled: isAuthenticated && !!categoryId,
    // Brak drabinki dla kategorii to 404 — nie ponawiamy.
    retry: false,
  })
}

/** Drabinki wszystkich podanych kategorii (te same klucze cache co useBracket). */
export function useBrackets(tournamentId: string, categoryIds: string[], enabled = true) {
  const { request, isAuthenticated } = useApi()
  return useQueries({
    queries: categoryIds.map((categoryId) => ({
      queryKey: matchKeys.bracket(tournamentId, categoryId),
      queryFn: () =>
        request<Bracket>(
          `${base(tournamentId)}/bracket?categoryId=${encodeURIComponent(categoryId)}`,
        ),
      enabled: isAuthenticated && enabled,
      retry: false,
    })),
  })
}

/** Usuwa drabinkę razem z jej (nierozpoczętymi) meczami — organizator układa pary od nowa. */
export function useResetBracket(tournamentId: string) {
  const { request } = useApi()
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ categoryId }: { categoryId: string }) =>
      request<{ matchesRemoved: number }>(
        `${base(tournamentId)}/bracket?categoryId=${encodeURIComponent(categoryId)}`,
        { method: "DELETE" },
      ),
    onSuccess: (_data, { categoryId }) => {
      // reset, nie invalidate: po 404 z odświeżenia zostałaby w cache stara drabinka.
      void queryClient.resetQueries({ queryKey: matchKeys.bracket(tournamentId, categoryId) })
      void queryClient.invalidateQueries({ queryKey: matchKeys.schedule(tournamentId) })
      // Puchar: backend cofa etap turnieju do "Zapisy zamknięte".
      void queryClient.invalidateQueries({ queryKey: tournamentKeys.detail(tournamentId) })
      void queryClient.invalidateQueries({ queryKey: tournamentKeys.dashboard(tournamentId) })
      void queryClient.invalidateQueries({ queryKey: ["tournaments", "list"] })
    },
  })
}

/**
 * Przelicza drabinkę po poprawce wyniku: zwycięzca każdego zakończonego meczu trafia tam, gdzie
 * powinien. Mecze, które już się zaczęły, zostają bez zmian i wracają w blockedMatchIds.
 */
export function useRecalculateBracket(tournamentId: string) {
  const { request } = useApi()
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ categoryId }: { categoryId: string }) =>
      request<{ changed: boolean; blockedMatchIds: string[] }>(
        `${base(tournamentId)}/bracket/recalculate?categoryId=${encodeURIComponent(categoryId)}`,
        { method: "POST" },
      ),
    onSuccess: (_data, { categoryId }) => {
      void queryClient.invalidateQueries({ queryKey: matchKeys.bracket(tournamentId, categoryId) })
      void queryClient.invalidateQueries({ queryKey: matchKeys.schedule(tournamentId) })
    },
  })
}

// BracketSlot: Home = 1, Away = 2.
const slotNumber = (slot: BracketSlotRef["slot"]) => (slot === "Home" ? 1 : 2)

/**
 * Zamienia miejscami dwa sloty pierwszej rundy (drużynę, miejsce w grupie albo wolny los). Działa,
 * dopóki żaden mecz drabinki się nie zaczął; terminy i boiska meczów zostają.
 */
export function useSwapBracketSlots(tournamentId: string) {
  const { request } = useApi()
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({
      categoryId,
      first,
      second,
    }: {
      categoryId: string
      first: BracketSlotRef
      second: BracketSlotRef
    }) =>
      request(`${base(tournamentId)}/bracket/swap-slots`, {
        method: "POST",
        body: {
          categoryId,
          firstNodeId: first.nodeId,
          firstSlot: slotNumber(first.slot),
          secondNodeId: second.nodeId,
          secondSlot: slotNumber(second.slot),
        },
      }),
    onSuccess: (_data, { categoryId }) => {
      void queryClient.invalidateQueries({ queryKey: matchKeys.bracket(tournamentId, categoryId) })
      // Mecze pierwszej rundy dostają inne drużyny, a przy wolnym losie powstają albo znikają.
      void queryClient.invalidateQueries({ queryKey: matchKeys.schedule(tournamentId) })
    },
  })
}

// --- Statystyki zawodników ----------------------------------------------------------------

const RANKING_PATH: Record<PlayerRankingKind, string> = {
  scorers: "top-scorers",
  assists: "top-assists",
  mvp: "mvp",
}

/** Ranking zawodników turnieju (pełne nazwiska, bez skracania jak w portalu publicznym). */
export function useTournamentRanking(tournamentId: string, kind: PlayerRankingKind) {
  const { request, isAuthenticated } = useApi()
  return useQuery({
    queryKey: matchKeys.ranking(tournamentId, kind),
    queryFn: () =>
      request<RankingEntry[]>(`${base(tournamentId)}/stats/${RANKING_PATH[kind]}?top=10`),
    enabled: isAuthenticated,
  })
}

// --- Tabele --------------------------------------------------------------------------------

export function useStandings(tournamentId: string, groupId: string) {
  const { request, isAuthenticated } = useApi()
  return useQuery({
    queryKey: matchKeys.standings(tournamentId, groupId),
    queryFn: () =>
      request<Standing[]>(`${base(tournamentId)}/standings?groupId=${encodeURIComponent(groupId)}`),
    // Drużyna bez grupy (np. w pucharze) nie ma tabeli.
    enabled: isAuthenticated && !!groupId,
  })
}

/**
 * Źródło slotu pierwszej rundy — BracketSourceType: Fixed = 1, GroupPosition = 2, Bye = 5 (wolny los:
 * rywal z pary przechodzi dalej bez meczu).
 */
export type BracketSlotInput =
  | { kind: "group"; groupId: string; position: number }
  | { kind: "team"; tournamentTeamId: string }
  | { kind: "bye" }

const slotBody = (prefix: "home" | "away", slot: BracketSlotInput) =>
  slot.kind === "group"
    ? {
        [`${prefix}SourceType`]: 2,
        [`${prefix}GroupId`]: slot.groupId,
        [`${prefix}Position`]: slot.position,
        [`${prefix}TournamentTeamId`]: null,
      }
    : {
        [`${prefix}SourceType`]: slot.kind === "bye" ? 5 : 1,
        [`${prefix}GroupId`]: null,
        [`${prefix}Position`]: null,
        [`${prefix}TournamentTeamId`]: slot.kind === "bye" ? null : slot.tournamentTeamId,
      }

export function useGenerateBracket(tournamentId: string) {
  const { request } = useApi()
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({
      categoryId,
      pairs,
    }: {
      categoryId: string
      pairs: { home: BracketSlotInput; away: BracketSlotInput }[]
    }) =>
      request(`${base(tournamentId)}/bracket/generate`, {
        method: "POST",
        body: {
          categoryId,
          roundOnePairings: pairs.map((p) => ({
            ...slotBody("home", p.home),
            ...slotBody("away", p.away),
          })),
        },
      }),
    onSuccess: (_data, { categoryId }) => {
      void queryClient.invalidateQueries({ queryKey: matchKeys.bracket(tournamentId, categoryId) })
      // Pary ze stałymi drużynami od razu dostają mecze.
      void queryClient.invalidateQueries({ queryKey: matchKeys.schedule(tournamentId) })
    },
  })
}
