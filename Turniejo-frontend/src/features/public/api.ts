"use client"

import { useQueries, useQuery } from "@tanstack/react-query"

import { apiFetch } from "@/lib/api/client"
import type { MatchLivePanel } from "@/types/matches"
import type {
  Bracket,
  PublicMatch,
  PublicSquadPlayer,
  PublicTeam,
  PublicTournament,
  PublicTournamentListItem,
  RankingEntry,
  Standing,
} from "@/types/public"

// Portal kibica działa bez logowania — zapytania idą bez tokenu, przez /backend (ten sam origin).
const base = (slug: string) => `/public/tournaments/${encodeURIComponent(slug)}`

export const publicKeys = {
  all: (slug: string) => ["public", slug] as const,
  tournament: (slug: string) => ["public", slug, "tournament"] as const,
  teams: (slug: string) => ["public", slug, "teams"] as const,
  schedule: (slug: string) => ["public", slug, "schedule"] as const,
  standings: (slug: string, groupId: string) => ["public", slug, "standings", groupId] as const,
  bracket: (slug: string, categoryId: string) => ["public", slug, "bracket", categoryId] as const,
  ranking: (slug: string, kind: RankingKind) => ["public", slug, "ranking", kind] as const,
  fullRanking: (slug: string, kind: RankingKind) =>
    ["public", slug, "ranking", kind, "all"] as const,
  squad: (slug: string, teamId: string) => ["public", slug, "squad", teamId] as const,
  liveMatch: (slug: string, matchId: string) => ["public", slug, "live", matchId] as const,
}

/** Lista turniejów publicznych. Odświeża się co minutę, żeby liczba meczów na żywo była aktualna. */
export function usePublicTournaments(initialData?: PublicTournamentListItem[]) {
  return useQuery({
    queryKey: ["public", "list"],
    queryFn: () => apiFetch<PublicTournamentListItem[]>("/public/tournaments"),
    initialData,
    refetchInterval: 60_000,
  })
}

export function usePublicTournament(slug: string, initialData?: PublicTournament) {
  return useQuery({
    queryKey: publicKeys.tournament(slug),
    queryFn: () => apiFetch<PublicTournament>(base(slug)),
    initialData,
    staleTime: 60_000,
  })
}

export function usePublicTeams(slug: string, enabled = true) {
  return useQuery({
    queryKey: publicKeys.teams(slug),
    queryFn: () => apiFetch<PublicTeam[]>(`${base(slug)}/teams`),
    enabled,
    staleTime: 60_000,
  })
}

export function usePublicSchedule(slug: string, enabled = true) {
  return useQuery({
    queryKey: publicKeys.schedule(slug),
    queryFn: () => apiFetch<PublicMatch[]>(`${base(slug)}/schedule`),
    enabled,
  })
}

export function usePublicStandings(slug: string, groupId: string, enabled = true) {
  return useQuery({
    queryKey: publicKeys.standings(slug, groupId),
    queryFn: () =>
      apiFetch<Standing[]>(`${base(slug)}/standings?groupId=${encodeURIComponent(groupId)}`),
    enabled: enabled && !!groupId,
  })
}

export function usePublicBracket(slug: string, categoryId: string | undefined) {
  return useQuery({
    queryKey: publicKeys.bracket(slug, categoryId ?? ""),
    queryFn: () =>
      apiFetch<Bracket>(`${base(slug)}/bracket?categoryId=${encodeURIComponent(categoryId!)}`),
    enabled: !!categoryId,
    // Brak drabinki (faza grupowa jeszcze trwa) to 404 — nie ponawiamy.
    retry: false,
  })
}

/** Drabinki wszystkich kategorii — do zapowiedzi meczów kolejnych rund w terminarzu. */
export function usePublicBrackets(slug: string, categoryIds: string[], enabled: boolean) {
  return useQueries({
    queries: categoryIds.map((categoryId) => ({
      queryKey: publicKeys.bracket(slug, categoryId),
      queryFn: () =>
        apiFetch<Bracket>(`${base(slug)}/bracket?categoryId=${encodeURIComponent(categoryId)}`),
      enabled,
      retry: false,
    })),
  })
}

export type RankingKind = "top-scorers" | "top-assists" | "mvp-ranking"

export function usePublicRanking(slug: string, kind: RankingKind) {
  return useQuery({
    queryKey: publicKeys.ranking(slug, kind),
    queryFn: () => apiFetch<RankingEntry[]>(`${base(slug)}/stats/${kind}?top=10`),
  })
}

/** Cały ranking (nie tylko pierwsza dziesiątka) — gole i asysty zawodników na stronie drużyny. */
export function usePublicFullRanking(slug: string, kind: RankingKind, enabled: boolean) {
  return useQuery({
    queryKey: publicKeys.fullRanking(slug, kind),
    queryFn: () => apiFetch<RankingEntry[]>(`${base(slug)}/stats/${kind}?top=1000`),
    enabled,
  })
}

/** Skład drużyny. Bez „Pokazuj zawodników” backend zwraca 404 — wtedy nie pytamy. */
export function usePublicSquad(slug: string, teamId: string, enabled: boolean) {
  return useQuery({
    queryKey: publicKeys.squad(slug, teamId),
    queryFn: () =>
      apiFetch<PublicSquadPlayer[]>(`${base(slug)}/teams/${encodeURIComponent(teamId)}/players`),
    enabled,
    retry: false,
  })
}

export type PublicLiveMatch = MatchLivePanel & { receivedAt: number }

/**
 * Szczegóły trwającego albo zakończonego meczu (zdarzenia, czas gry). elapsedSeconds liczy serwer —
 * klient dolicza tylko czas od odpowiedzi (receivedAt), więc zegar telefonu kibica nie ma znaczenia.
 */
export function usePublicLiveMatch(slug: string, matchId: string, enabled: boolean) {
  return useQuery({
    queryKey: publicKeys.liveMatch(slug, matchId),
    queryFn: async (): Promise<PublicLiveMatch> => ({
      ...(await apiFetch<MatchLivePanel>(`/public/matches/${matchId}/live`)),
      receivedAt: Date.now(),
    }),
    enabled,
    retry: false,
  })
}
