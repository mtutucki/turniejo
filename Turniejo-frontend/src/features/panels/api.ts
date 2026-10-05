"use client"

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query"

import { useApi } from "@/hooks/use-api"
import { PlayerPosition, type TournamentPlayer } from "@/types/teams"
import {
  type MyChild,
  type MyChildMatch,
  type MyChildStanding,
  type MyChildTeam,
  type MyTeam,
  type TeamMatch,
  type TeamPlayerStats,
} from "@/types/roles-panels"

export const panelKeys = {
  children: ["parent", "children"] as const,
  child: (playerId: string, part: "team" | "schedule" | "results" | "standing") =>
    ["parent", "children", playerId, part] as const,
  myTeams: ["coach", "teams"] as const,
  team: (tournamentTeamId: string, part: "players" | "schedule" | "results" | "stats") =>
    ["coach", "teams", tournamentTeamId, part] as const,
}

function useGet<T>(key: readonly unknown[], path: string, enabled = true) {
  const { request, isAuthenticated } = useApi()
  return useQuery({
    queryKey: key,
    queryFn: () => request<T>(path),
    enabled: isAuthenticated && enabled,
    // Cudze dziecko / cudza drużyna to 403 — nie ponawiamy, pokazujemy komunikat.
    retry: false,
  })
}

// --- Rodzic --------------------------------------------------------------------------------

export const useMyChildren = () => useGet<MyChild[]>(panelKeys.children, "/api/parent/players")

export const useChildTeams = (playerId: string) =>
  useGet<MyChildTeam[]>(panelKeys.child(playerId, "team"), `/api/parent/players/${playerId}/team`)

export const useChildSchedule = (playerId: string) =>
  useGet<MyChildMatch[]>(
    panelKeys.child(playerId, "schedule"),
    `/api/parent/players/${playerId}/schedule`,
  )

export const useChildResults = (playerId: string) =>
  useGet<MyChildMatch[]>(
    panelKeys.child(playerId, "results"),
    `/api/parent/players/${playerId}/results`,
  )

export const useChildStanding = (playerId: string) =>
  useGet<MyChildStanding[]>(
    panelKeys.child(playerId, "standing"),
    `/api/parent/players/${playerId}/standing`,
  )

export type ChildProfileInput = {
  firstName: string
  lastName: string
  birthDate: string
  defaultPosition: keyof typeof PlayerPosition
  photoUrl: string | null
}

/** Edycja profilu dziecka — backend (PlayerAccessGuard) wpuszcza tylko opiekuna z CanEdit=true. */
export function useUpdateChild(playerId: string) {
  const { request } = useApi()
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (input: ChildProfileInput) =>
      request(`/api/parent/players/${playerId}`, {
        method: "PATCH",
        body: { ...input, defaultPosition: PlayerPosition[input.defaultPosition] },
      }),
    meta: { handlesValidation: true },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: panelKeys.children }),
  })
}

// --- Trener --------------------------------------------------------------------------------

export const useMyTeams = () => useGet<MyTeam[]>(panelKeys.myTeams, "/api/coach/teams")

export const useCoachPlayers = (tournamentTeamId: string) =>
  useGet<TournamentPlayer[]>(
    panelKeys.team(tournamentTeamId, "players"),
    `/api/coach/teams/${tournamentTeamId}/players`,
  )

export const useCoachSchedule = (tournamentTeamId: string) =>
  useGet<TeamMatch[]>(
    panelKeys.team(tournamentTeamId, "schedule"),
    `/api/coach/teams/${tournamentTeamId}/schedule`,
  )

export const useCoachResults = (tournamentTeamId: string) =>
  useGet<TeamMatch[]>(
    panelKeys.team(tournamentTeamId, "results"),
    `/api/coach/teams/${tournamentTeamId}/results`,
  )

export const useCoachStats = (tournamentTeamId: string) =>
  useGet<TeamPlayerStats[]>(
    panelKeys.team(tournamentTeamId, "stats"),
    `/api/coach/teams/${tournamentTeamId}/player-stats`,
  )

// --- Opiekunowie zawodnika (organizator) ---------------------------------------------------

/**
 * Przypisanie opiekuna (rodzica) do zawodnika. Listy opiekunów organizator nie odczyta (GET guardians
 * jest tylko dla samych opiekunów) — duplikat odrzuca backend ("User is already a guardian").
 */
export function useAssignGuardian(playerId: string) {
  const { request } = useApi()
  return useMutation({
    mutationFn: (input: {
      tournamentId: string
      tournamentTeamId: string
      userId: string
      relationshipType: "Parent" | "Guardian" | "Other"
      canEdit: boolean
    }) =>
      request(`/api/players/${playerId}/guardians`, {
        method: "POST",
        body: {
          ...input,
          relationshipType: { Parent: 1, Guardian: 2, Other: 3 }[input.relationshipType],
          canReceiveNotifications: true,
        },
      }),
    meta: { handlesValidation: true },
  })
}
