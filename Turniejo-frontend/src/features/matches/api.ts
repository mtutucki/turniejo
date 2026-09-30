"use client"

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query"

import { tournamentKeys } from "@/features/tournaments/api"
import { useApi } from "@/hooks/use-api"
import type { GenerateScheduleInput, Match, Pitch, PitchInput } from "@/types/matches"
import type { Bracket } from "@/types/public"

export const matchKeys = {
  pitches: (tournamentId: string) => ["tournaments", tournamentId, "pitches"] as const,
  schedule: (tournamentId: string) => ["tournaments", tournamentId, "schedule"] as const,
  bracket: (tournamentId: string, categoryId: string) =>
    ["tournaments", tournamentId, "bracket", categoryId] as const,
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
            body: { name: input.name, description: input.description },
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
      void queryClient.invalidateQueries({ queryKey: tournamentKeys.dashboard(tournamentId) })
      void queryClient.invalidateQueries({ queryKey: ["tournaments", "list"] })
    },
  })
}

function useInvalidateMatches(tournamentId: string) {
  const queryClient = useQueryClient()
  return () => {
    void queryClient.invalidateQueries({ queryKey: matchKeys.schedule(tournamentId) })
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
    }: {
      matchId: string
      pitchId: string | null
      scheduledAt: string
    }) =>
      request(`${base(tournamentId)}/matches/${matchId}/schedule`, {
        method: "PUT",
        body: { pitchId, scheduledAt },
      }),
    meta: { handlesValidation: true },
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

/** Źródło slotu pierwszej rundy — BracketSourceType: Fixed = 1, GroupPosition = 2. */
export type BracketSlotInput =
  { kind: "group"; groupId: string; position: number } | { kind: "team"; tournamentTeamId: string }

const slotBody = (prefix: "home" | "away", slot: BracketSlotInput) =>
  slot.kind === "group"
    ? {
        [`${prefix}SourceType`]: 2,
        [`${prefix}GroupId`]: slot.groupId,
        [`${prefix}Position`]: slot.position,
        [`${prefix}TournamentTeamId`]: null,
      }
    : {
        [`${prefix}SourceType`]: 1,
        [`${prefix}GroupId`]: null,
        [`${prefix}Position`]: null,
        [`${prefix}TournamentTeamId`]: slot.tournamentTeamId,
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
