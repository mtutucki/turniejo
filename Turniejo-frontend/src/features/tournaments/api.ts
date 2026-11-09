"use client"

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query"

import { useApi } from "@/hooks/use-api"
import { myRolesQueryKey } from "@/hooks/use-my-roles"
import {
  type CategoryInput,
  type MyOrganization,
  TieBreakerType,
  type TieBreakerRule,
  type Tournament,
  type TournamentCategory,
  type TournamentDashboard,
  type TournamentDetailsInput,
  TournamentFormat,
  type TournamentGroup,
  type TournamentSettings,
  TournamentStatus,
} from "@/types/tournaments"
import type { PublicSettings } from "@/types/public"

export const tournamentKeys = {
  myOrganization: ["organizations", "mine"] as const,
  list: (organizationId: string) => ["tournaments", "list", organizationId] as const,
  detail: (id: string) => ["tournaments", id] as const,
  dashboard: (id: string) => ["tournaments", id, "dashboard"] as const,
  settings: (id: string) => ["tournaments", id, "settings"] as const,
  categories: (id: string) => ["tournaments", id, "categories"] as const,
  groups: (id: string) => ["tournaments", id, "groups"] as const,
  tieBreakerRules: (id: string) => ["tournaments", id, "tiebreaker-rules"] as const,
  publicSettings: (id: string) => ["tournaments", id, "public-settings"] as const,
}

const base = (id: string) => `/api/tournaments/${id}`

// --- Queries ---------------------------------------------------------------------------------

/** Ile razy dopytujemy o organizację, zanim uznamy, że konto jej nie ma (~30 s co 1,5 s). */
const ORGANIZATION_POLL_LIMIT = 20
// Outbox backendu chodzi co sekundę — rzadsze dopytywanie tylko wydłużało ekran oczekiwania.
const ORGANIZATION_POLL_INTERVAL_MS = 1_500
const PROVISIONING_KEY = "turniejo:organization-provisioning"

/** Rejestracja jako organizator: organizacja dopiero powstaje, więc brak danych ≠ "nie masz organizacji". */
export function markOrganizationProvisioning() {
  try {
    sessionStorage.setItem(PROVISIONING_KEY, "1")
  } catch {
    // Brak sessionStorage (tryb prywatny) — najwyżej mignie pusty stan.
  }
}

export function clearOrganizationProvisioning() {
  try {
    sessionStorage.removeItem(PROVISIONING_KEY)
  } catch {
    // Brak sessionStorage — nie ma czego czyścić.
  }
}

/** Czy wciąż czekamy na organizację zakładaną po rejestracji (zamiast pokazywać "Zostań organizatorem"). */
export function isOrganizationProvisioning(dataUpdateCount: number) {
  if (dataUpdateCount >= ORGANIZATION_POLL_LIMIT) return false
  try {
    return sessionStorage.getItem(PROVISIONING_KEY) !== null
  } catch {
    return false
  }
}

export function useMyOrganization() {
  const { request, isAuthenticated } = useApi()
  return useQuery({
    queryKey: tournamentKeys.myOrganization,
    // 204 (brak organizacji) → undefined; React Query nie przyjmuje undefined jako danych.
    queryFn: async () =>
      (await request<MyOrganization | undefined>("/api/organizations/mine")) ?? null,
    enabled: isAuthenticated,
    staleTime: Infinity,
    // Organizacja organizatora powstaje asynchronicznie po rejestracji (outbox backendu) — tuż po
    // założeniu konta dopytujemy przez ~30 s, zamiast pokazywać "nie masz organizacji" do odświeżenia.
    refetchInterval: (query) =>
      query.state.data === null && query.state.dataUpdateCount < ORGANIZATION_POLL_LIMIT
        ? ORGANIZATION_POLL_INTERVAL_MS
        : false,
  })
}

/** "Zostań organizatorem" — konto rodzica/trenera/sędziego zakłada własną organizację. */
export function useCreateOrganization() {
  const { request } = useApi()
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (name: string) =>
      request<{ organizationId: string; slug: string }>("/api/organizations", {
        method: "POST",
        body: { name },
      }),
    meta: { handlesValidation: true },
    onSuccess: async () => {
      // Nowa rola Organizer (menu "Moje turnieje", "Baza drużyn") i sama organizacja.
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: myRolesQueryKey }),
        queryClient.invalidateQueries({ queryKey: tournamentKeys.myOrganization }),
      ])
    },
  })
}

/** PUT /api/organizations/{id} — zmiana nazwy organizacji (tylko organizator). */
export function useRenameOrganization(organizationId: string) {
  const { request } = useApi()
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (name: string) =>
      request(`/api/organizations/${organizationId}`, { method: "PUT", body: { name } }),
    meta: { handlesValidation: true },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: tournamentKeys.myOrganization }),
  })
}

export function useTournaments(organizationId: string | undefined) {
  const { request, isAuthenticated } = useApi()
  return useQuery({
    queryKey: tournamentKeys.list(organizationId ?? ""),
    queryFn: () => request<Tournament[]>(`/api/tournaments?organizationId=${organizationId}`),
    enabled: isAuthenticated && !!organizationId,
  })
}

export function useTournament(id: string) {
  const { request, isAuthenticated } = useApi()
  return useQuery({
    queryKey: tournamentKeys.detail(id),
    queryFn: () => request<Tournament>(base(id)),
    enabled: isAuthenticated,
  })
}

export function useTournamentDashboard(id: string) {
  const { request, isAuthenticated } = useApi()
  return useQuery({
    queryKey: tournamentKeys.dashboard(id),
    queryFn: () => request<TournamentDashboard>(`${base(id)}/dashboard?upcomingMatchesCount=5`),
    enabled: isAuthenticated,
  })
}

export function useTournamentSettings(id: string) {
  const { request, isAuthenticated } = useApi()
  return useQuery({
    queryKey: tournamentKeys.settings(id),
    queryFn: () => request<TournamentSettings>(`${base(id)}/settings`),
    enabled: isAuthenticated,
  })
}

export function useCategories(id: string) {
  const { request, isAuthenticated } = useApi()
  return useQuery({
    queryKey: tournamentKeys.categories(id),
    queryFn: () => request<TournamentCategory[]>(`${base(id)}/categories`),
    enabled: isAuthenticated,
    select: (categories) => [...categories].sort((a, b) => a.order - b.order),
  })
}

export function useGroups(id: string) {
  const { request, isAuthenticated } = useApi()
  return useQuery({
    queryKey: tournamentKeys.groups(id),
    queryFn: () => request<TournamentGroup[]>(`${base(id)}/groups`),
    enabled: isAuthenticated,
    select: (groups) => [...groups].sort((a, b) => a.order - b.order),
  })
}

export function useTieBreakerRules(id: string) {
  const { request, isAuthenticated } = useApi()
  return useQuery({
    queryKey: tournamentKeys.tieBreakerRules(id),
    queryFn: () => request<TieBreakerRule[]>(`${base(id)}/tiebreaker-rules`),
    enabled: isAuthenticated,
  })
}

// --- Mutations -------------------------------------------------------------------------------

export function useCreateTournament() {
  const { request } = useApi()
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (input: TournamentDetailsInput & { organizationId: string }) =>
      request<{ tournamentId: string; slug: string }>("/api/tournaments", {
        method: "POST",
        body: input,
      }),
    meta: { handlesValidation: true },
    onSuccess: (_, { organizationId }) => {
      void queryClient.invalidateQueries({ queryKey: tournamentKeys.list(organizationId) })
      // Twórca dostaje rolę Organizer w nowym turnieju.
      void queryClient.invalidateQueries({ queryKey: myRolesQueryKey })
    },
  })
}

/** Unieważnia wszystko, co pokazuje dane turnieju (detal, dashboard, listy). */
function useInvalidateTournament(id: string) {
  const queryClient = useQueryClient()
  return () => {
    void queryClient.invalidateQueries({ queryKey: tournamentKeys.detail(id) })
    void queryClient.invalidateQueries({ queryKey: ["tournaments", "list"] })
  }
}

export function useUpdateTournamentDetails(id: string) {
  const { request } = useApi()
  const invalidate = useInvalidateTournament(id)
  return useMutation({
    mutationFn: (input: TournamentDetailsInput) =>
      request(base(id), { method: "PUT", body: input }),
    meta: { handlesValidation: true },
    onSuccess: invalidate,
  })
}

export function useChangeTournamentStatus(id: string) {
  const { request } = useApi()
  const invalidate = useInvalidateTournament(id)
  return useMutation({
    mutationFn: (status: TournamentStatus) =>
      request(`${base(id)}/status`, {
        method: "PATCH",
        body: { tournamentStatus: TournamentStatus[status] },
      }),
    onSuccess: invalidate,
  })
}

/** Usunięcie anulowanego turnieju (miękkie — znika z list, szczegółów i portalu). */
export function useDeleteTournament(id: string) {
  const { request } = useApi()
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: () => request(base(id), { method: "DELETE" }),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ["tournaments", "list"] })
      queryClient.removeQueries({ queryKey: tournamentKeys.detail(id) })
    },
  })
}

export function useChangeTournamentFormat(id: string) {
  const { request } = useApi()
  const invalidate = useInvalidateTournament(id)
  return useMutation({
    mutationFn: (format: TournamentFormat) =>
      request(`${base(id)}/format`, {
        method: "PATCH",
        body: { tournamentFormat: TournamentFormat[format] },
      }),
    // invalidate() odświeża też ["tournaments", id, ...] — grupy i drużyny, które backend mógł
    // właśnie utworzyć/przypisać (domyślna "Grupa A" po wyborze formatu z grupami).
    onSuccess: invalidate,
  })
}

export function useUpdateTournamentSettings(id: string) {
  const { request } = useApi()
  const queryClient = useQueryClient()
  return useMutation({
    // expected: wartości z chwili otwarcia formularza - serwer odrzuca zapis, gdy ktoś zmienił je w międzyczasie.
    mutationFn: ({ expected, ...settings }: TournamentSettings & { expected?: TournamentSettings }) =>
      request(`${base(id)}/settings`, { method: "PUT", body: { ...settings, expected } }),
    meta: { handlesValidation: true },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: tournamentKeys.settings(id) }),
  })
}

/** Ustawienia portalu kibica — tylko organizator (GET/PUT /public-settings). */
export function usePublicSettings(id: string, enabled: boolean) {
  const { request, isAuthenticated } = useApi()
  return useQuery({
    queryKey: tournamentKeys.publicSettings(id),
    queryFn: () => request<PublicSettings>(`${base(id)}/public-settings`),
    enabled: isAuthenticated && enabled,
  })
}

export function useUpdatePublicSettings(id: string) {
  const { request } = useApi()
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ expected, ...settings }: PublicSettings & { expected?: PublicSettings }) =>
      request(`${base(id)}/public-settings`, { method: "PUT", body: { ...settings, expected } }),
    onSuccess: () =>
      Promise.all([
        queryClient.invalidateQueries({ queryKey: tournamentKeys.publicSettings(id) }),
        // TournamentDto.isPublic — szczegóły turnieju też się zmieniły.
        queryClient.invalidateQueries({ queryKey: tournamentKeys.detail(id), exact: true }),
      ]),
  })
}

export function useSaveCategory(id: string) {
  const { request } = useApi()
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ categoryId, ...input }: CategoryInput & { categoryId?: string }) =>
      categoryId
        ? request(`${base(id)}/categories/${categoryId}`, { method: "PUT", body: input })
        : request(`${base(id)}/categories`, { method: "POST", body: input }),
    meta: { handlesValidation: true },
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: tournamentKeys.categories(id) })
      // Format z grupami: nowa kategoria dostaje od razu "Grupa A" (DefaultGroupProvisioner).
      void queryClient.invalidateQueries({ queryKey: tournamentKeys.groups(id) })
    },
  })
}

export function useDeleteCategory(id: string) {
  const { request } = useApi()
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (categoryId: string) =>
      request(`${base(id)}/categories/${categoryId}`, { method: "DELETE" }),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: tournamentKeys.categories(id) })
      void queryClient.invalidateQueries({ queryKey: tournamentKeys.groups(id) })
    },
  })
}

export function useSaveGroup(id: string) {
  const { request } = useApi()
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({
      groupId,
      categoryId,
      name,
      order,
    }: {
      groupId?: string
      categoryId: string
      name: string
      order: number
    }) =>
      groupId
        ? request(`${base(id)}/groups/${groupId}`, { method: "PUT", body: { name, order } })
        : request(`${base(id)}/groups`, { method: "POST", body: { categoryId, name, order } }),
    meta: { handlesValidation: true },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: tournamentKeys.groups(id) }),
  })
}

export function useDeleteGroup(id: string) {
  const { request } = useApi()
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (groupId: string) => request(`${base(id)}/groups/${groupId}`, { method: "DELETE" }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: tournamentKeys.groups(id) }),
  })
}

export function useSetTieBreakerRules(id: string) {
  const { request } = useApi()
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (types: TieBreakerType[]) =>
      request(`${base(id)}/tiebreaker-rules`, {
        method: "PUT",
        body: {
          rules: types.map((type, index) => ({ order: index + 1, type: TieBreakerType[type] })),
        },
      }),
    onSuccess: () =>
      queryClient.invalidateQueries({ queryKey: tournamentKeys.tieBreakerRules(id) }),
  })
}
