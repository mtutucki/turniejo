"use client"

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query"

import { useApi } from "@/hooks/use-api"
import { apiFetch } from "@/lib/api/client"
import type { SecondStage, SecondStageSeatInput } from "@/types/tournaments"

export const secondStageKeys = {
  panel: (id: string) => ["tournaments", id, "second-stage"] as const,
  public: (slug: string) => ["public", slug, "second-stage"] as const,
}

const EMPTY: SecondStage = { seats: [], fixtures: [] }

/** Podział na grupy fazy 2 i zaplanowane mecze (format „Dwie fazy grupowe”). */
export function useSecondStage(id: string, enabled: boolean) {
  const { request, isAuthenticated } = useApi()
  return useQuery({
    queryKey: secondStageKeys.panel(id),
    queryFn: () => request<SecondStage>(`/api/tournaments/${id}/second-stage`),
    enabled: isAuthenticated && enabled,
    placeholderData: enabled ? undefined : EMPTY,
  })
}

export function usePublicSecondStage(slug: string, enabled: boolean) {
  return useQuery({
    queryKey: secondStageKeys.public(slug),
    queryFn: () =>
      apiFetch<SecondStage>(`/public/tournaments/${encodeURIComponent(slug)}/second-stage`),
    enabled,
    placeholderData: enabled ? undefined : EMPTY,
  })
}

/** Zastępuje cały podział kategorii (SetSecondStageSeatsCommand). */
export function useSaveSecondStageSeats(id: string) {
  const { request } = useApi()
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ categoryId, seats }: { categoryId: string; seats: SecondStageSeatInput[] }) =>
      request(`/api/tournaments/${id}/second-stage/seats`, {
        method: "PUT",
        body: { categoryId, seats },
      }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: secondStageKeys.panel(id) }),
  })
}
