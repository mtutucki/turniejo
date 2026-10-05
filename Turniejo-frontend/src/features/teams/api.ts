"use client"

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query"

import { tournamentKeys } from "@/features/tournaments/api"
import { useApi } from "@/hooks/use-api"
import { myRolesQueryKey } from "@/hooks/use-my-roles"
import { TOURNAMENT_PLAYER_STATUS_VALUES } from "@/types/roles-panels"
import {
  type ImportSquadInput,
  type JoinResult,
  type NewPlayerInput,
  PaymentStatus,
  PlayerPosition,
  RegistrationStatus,
  type RosterSource,
  type SquadPlayerInput,
  type Team,
  type TeamInput,
  type TeamStaffMember,
  TeamStaffRole,
  type TournamentMember,
  type TournamentPlayer,
  type TournamentTeam,
} from "@/types/teams"

export const teamKeys = {
  organizationTeams: (organizationId: string) =>
    ["organizations", organizationId, "teams"] as const,
  tournamentTeams: (tournamentId: string) => ["tournaments", tournamentId, "teams"] as const,
  players: (tournamentTeamId: string) => ["tournament-teams", tournamentTeamId, "players"] as const,
  rosterSources: (tournamentTeamId: string) =>
    ["tournament-teams", tournamentTeamId, "roster-sources"] as const,
  staff: (tournamentTeamId: string) => ["tournament-teams", tournamentTeamId, "staff"] as const,
  members: (tournamentId: string) => ["tournaments", tournamentId, "members"] as const,
}

const tournamentTeamPath = (tournamentId: string, tournamentTeamId: string) =>
  `/api/tournaments/${tournamentId}/tournament-teams/${tournamentTeamId}`

// --- Drużyny organizacji -------------------------------------------------------------------

export function useOrganizationTeams(organizationId: string | undefined) {
  const { request, isAuthenticated } = useApi()
  return useQuery({
    queryKey: teamKeys.organizationTeams(organizationId ?? ""),
    queryFn: () => request<Team[]>(`/api/organizations/${organizationId}/teams`),
    enabled: isAuthenticated && !!organizationId,
  })
}

export function useSaveTeam(organizationId: string) {
  const { request } = useApi()
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async ({ teamId, ...input }: TeamInput & { teamId?: string }) => {
      const base = `/api/organizations/${organizationId}/teams`
      if (!teamId) return request(base, { method: "POST", body: input })
      // Edycja: nazwa/skrót i logo to osobne endpointy (UpdateTeam nie przyjmuje LogoUrl).
      await request(`${base}/${teamId}`, {
        method: "PUT",
        body: { name: input.name, shortName: input.shortName },
      })
      return request(`${base}/${teamId}/logo`, { method: "PUT", body: { logoUrl: input.logoUrl } })
    },
    meta: { handlesValidation: true },
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: teamKeys.organizationTeams(organizationId) })
      // Nazwy drużyn są też w listach zgłoszeń turniejowych.
      void queryClient.invalidateQueries({ queryKey: ["tournaments"] })
    },
  })
}

// --- Drużyny w turnieju --------------------------------------------------------------------

export function useTournamentTeams(tournamentId: string) {
  const { request, isAuthenticated } = useApi()
  return useQuery({
    queryKey: teamKeys.tournamentTeams(tournamentId),
    queryFn: () => request<TournamentTeam[]>(`/api/tournaments/${tournamentId}/teams`),
    enabled: isAuthenticated,
    select: (teams) => [...teams].sort((a, b) => a.teamName.localeCompare(b.teamName, "pl")),
  })
}

/** Zgłoszenia zmieniają też statystyki dashboardu turnieju. */
function useInvalidateTournamentTeams(tournamentId: string) {
  const queryClient = useQueryClient()
  return () => {
    void queryClient.invalidateQueries({ queryKey: teamKeys.tournamentTeams(tournamentId) })
    void queryClient.invalidateQueries({ queryKey: tournamentKeys.dashboard(tournamentId) })
  }
}

export function useAddTeamToTournament(tournamentId: string) {
  const { request } = useApi()
  const invalidate = useInvalidateTournamentTeams(tournamentId)
  return useMutation({
    mutationFn: (input: { teamId: string; categoryId: string }) =>
      request<{ tournamentTeamId: string }>(`/api/tournaments/${tournamentId}/teams`, {
        method: "POST",
        body: input,
      }),
    meta: { handlesValidation: true },
    onSuccess: invalidate,
  })
}

export function useChangeRegistrationStatus(tournamentId: string) {
  const { request } = useApi()
  const invalidate = useInvalidateTournamentTeams(tournamentId)
  return useMutation({
    mutationFn: ({
      tournamentTeamId,
      status,
    }: {
      tournamentTeamId: string
      status: RegistrationStatus
    }) =>
      request(`${tournamentTeamPath(tournamentId, tournamentTeamId)}/registration-status`, {
        method: "PATCH",
        body: { registrationStatus: RegistrationStatus[status] },
      }),
    onSuccess: invalidate,
  })
}

export function useChangePaymentStatus(tournamentId: string) {
  const { request } = useApi()
  const invalidate = useInvalidateTournamentTeams(tournamentId)
  return useMutation({
    mutationFn: ({
      tournamentTeamId,
      status,
    }: {
      tournamentTeamId: string
      status: PaymentStatus
    }) =>
      request(`${tournamentTeamPath(tournamentId, tournamentTeamId)}/payment-status`, {
        method: "PATCH",
        body: { paymentStatus: PaymentStatus[status] },
      }),
    onSuccess: invalidate,
  })
}

export function useAssignTeamToGroup(tournamentId: string) {
  const { request } = useApi()
  const invalidate = useInvalidateTournamentTeams(tournamentId)
  return useMutation({
    mutationFn: ({
      tournamentTeamId,
      groupId,
    }: {
      tournamentTeamId: string
      groupId: string | null
    }) =>
      request(`${tournamentTeamPath(tournamentId, tournamentTeamId)}/group`, {
        method: "PATCH",
        body: { groupId },
      }),
    onSuccess: invalidate,
  })
}

// --- Skład ---------------------------------------------------------------------------------

export function useTeamPlayers(tournamentId: string, tournamentTeamId: string) {
  const { request, isAuthenticated } = useApi()
  return useQuery({
    queryKey: teamKeys.players(tournamentTeamId),
    queryFn: () =>
      request<TournamentPlayer[]>(`${tournamentTeamPath(tournamentId, tournamentTeamId)}/players`),
    enabled: isAuthenticated,
    select: (players) =>
      [...players].sort(
        (a, b) =>
          (a.shirtNumber ?? 999) - (b.shirtNumber ?? 999) ||
          a.lastName.localeCompare(b.lastName, "pl"),
      ),
  })
}

/**
 * Nowy zawodnik w składzie: profil (POST /players) + przypisanie do drużyny. Backend nie ma listy
 * zawodników do wyboru, więc profil zakładamy w tym samym kroku.
 */
export function useAddNewPlayerToSquad(tournamentId: string, tournamentTeamId: string) {
  const { request } = useApi()
  return useMutation({
    // Jedno wywołanie = jedna transakcja: profil powstaje tylko razem z wpisem w składzie.
    mutationFn: (input: NewPlayerInput) =>
      request<{ playerId: string; tournamentPlayerId: string }>(
        `${tournamentTeamPath(tournamentId, tournamentTeamId)}/players/new`,
        {
          method: "POST",
          body: {
            firstName: input.firstName,
            lastName: input.lastName,
            birthDate: input.birthDate,
            defaultPosition: PlayerPosition[input.position],
            photoUrl: input.photoUrl,
            shirtNumber: input.shirtNumber,
            position: PlayerPosition[input.position],
          },
        },
      ),
    meta: { handlesValidation: true },
    onSuccess: useInvalidateSquad(tournamentTeamId),
  })
}

function useInvalidateSquad(tournamentTeamId: string) {
  const queryClient = useQueryClient()
  return () => {
    void queryClient.invalidateQueries({ queryKey: teamKeys.players(tournamentTeamId) })
    // Flagi "już w składzie" na liście do skopiowania zależą od bieżącego składu.
    void queryClient.invalidateQueries({ queryKey: teamKeys.rosterSources(tournamentTeamId) })
    // Skład w panelu trenera (panelKeys.team) — literał, bo features/panels importuje ten moduł.
    void queryClient.invalidateQueries({ queryKey: ["coach", "teams", tournamentTeamId] })
  }
}

/** Edycja zawodnika już dodanego do składu — profil i wpis w składzie w jednej transakcji. */
export function useUpdatePlayerInSquad(tournamentId: string, tournamentTeamId: string) {
  const { request } = useApi()
  return useMutation({
    mutationFn: ({
      tournamentPlayerId,
      position,
      ...input
    }: Omit<SquadPlayerInput, "position"> & {
      tournamentPlayerId: string
      position: PlayerPosition | null
    }) =>
      request(
        `${tournamentTeamPath(tournamentId, tournamentTeamId)}/players/${tournamentPlayerId}`,
        {
          method: "PUT",
          body: {
            firstName: input.firstName,
            lastName: input.lastName,
            birthDate: input.birthDate,
            photoUrl: input.photoUrl,
            shirtNumber: input.shirtNumber,
            position: position ? PlayerPosition[position] : null,
            status: TOURNAMENT_PLAYER_STATUS_VALUES[input.status],
          },
        },
      ),
    meta: { handlesValidation: true },
    onSuccess: useInvalidateSquad(tournamentTeamId),
  })
}

/** Składy tej samej drużyny z innych turniejów — tylko dla osób, które mogą edytować skład. */
export function useRosterSources(tournamentId: string, tournamentTeamId: string, enabled = true) {
  const { request, isAuthenticated } = useApi()
  return useQuery({
    queryKey: teamKeys.rosterSources(tournamentTeamId),
    queryFn: () =>
      request<RosterSource[]>(
        `${tournamentTeamPath(tournamentId, tournamentTeamId)}/roster-sources`,
      ),
    enabled: isAuthenticated && enabled,
  })
}

/** Kopia wybranych zawodników z poprzedniego składu — wchodzi cała lista albo nic. */
export function useImportSquad(tournamentId: string, tournamentTeamId: string) {
  const { request } = useApi()
  return useMutation({
    mutationFn: (input: ImportSquadInput) =>
      request<{ tournamentPlayerIds: string[] }>(
        `${tournamentTeamPath(tournamentId, tournamentTeamId)}/players/import`,
        {
          method: "POST",
          body: {
            sourceTournamentTeamId: input.sourceTournamentTeamId,
            players: input.players.map((p) => ({
              playerId: p.playerId,
              shirtNumber: p.shirtNumber,
              position: p.position ? PlayerPosition[p.position] : null,
            })),
          },
        },
      ),
    meta: { handlesValidation: true },
    onSuccess: useInvalidateSquad(tournamentTeamId),
  })
}

// --- Staff ---------------------------------------------------------------------------------

export function useTeamStaff(tournamentId: string, tournamentTeamId: string, enabled = true) {
  const { request, isAuthenticated } = useApi()
  return useQuery({
    queryKey: teamKeys.staff(tournamentTeamId),
    queryFn: () =>
      request<TeamStaffMember[]>(`${tournamentTeamPath(tournamentId, tournamentTeamId)}/staff`),
    enabled: isAuthenticated && enabled,
  })
}

export function useTournamentMembers(tournamentId: string, enabled = true) {
  const { request, isAuthenticated } = useApi()
  return useQuery({
    queryKey: teamKeys.members(tournamentId),
    queryFn: () => request<TournamentMember[]>(`/api/tournaments/${tournamentId}/members`),
    enabled: isAuthenticated && enabled,
  })
}

export function useAssignStaff(tournamentId: string, tournamentTeamId: string) {
  const { request } = useApi()
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ userId, role }: { userId: string; role: TeamStaffRole }) =>
      request(`${tournamentTeamPath(tournamentId, tournamentTeamId)}/staff`, {
        method: "POST",
        body: { userId, role: TeamStaffRole[role] },
      }),
    meta: { handlesValidation: true },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: teamKeys.staff(tournamentTeamId) }),
  })
}

// --- Dołączanie kodem ----------------------------------------------------------------------

export function useJoinTournament() {
  const { request } = useApi()
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (code: string) =>
      request<JoinResult>("/api/tournaments/join", { method: "POST", body: { code } }),
    meta: { handlesValidation: true },
    // Nowa rola w turnieju — odśwież uprawnienia (useMyRoles / RoleGate).
    onSuccess: () => queryClient.invalidateQueries({ queryKey: myRolesQueryKey }),
  })
}
