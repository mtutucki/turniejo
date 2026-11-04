"use client"

import type { Session } from "next-auth"
import { useSession } from "next-auth/react"
import { useCallback } from "react"

import { apiFetch } from "@/lib/api/client"

// Access token z Keycloaka żyje 5 min — odświeżamy go przed requestem, zanim wygaśnie.
const EXPIRY_MARGIN_MS = 45_000

// Wiele równoległych zapytań po wygaśnięciu tokenu ma wywołać tylko jedno odświeżenie sesji.
let pendingRefresh: Promise<Session | null> | null = null

/** Zwraca funkcję `request`, która dołącza aktualny Bearer token do każdego zapytania. */
export function useApi() {
  const { data: session, status, update } = useSession()

  const getToken = useCallback(async () => {
    if (!session?.accessToken) return undefined
    if (
      session.accessTokenExpiresAt &&
      Date.now() < session.accessTokenExpiresAt - EXPIRY_MARGIN_MS
    ) {
      return session.accessToken
    }
    // update() woła /api/auth/session → callback jwt w auth.ts odświeża token refresh_tokenem.
    pendingRefresh ??= update().finally(() => {
      pendingRefresh = null
    })
    const refreshed = await pendingRefresh
    return refreshed?.accessToken
  }, [session, update])

  const request = useCallback(
    async <T>(path: string, options?: Omit<Parameters<typeof apiFetch>[1], "token">) =>
      apiFetch<T>(path, { ...options, token: await getToken() }),
    [getToken],
  )

  // getToken też dla SignalR (accessTokenFactory) — hub dostaje token w query stringu.
  // Sesja z błędem odświeżenia (np. po restarcie Keycloaka) ma nieważny token — zapytania wysłane z nim
  // kończyły się 401 i przekierowaniem na logowanie w kółko. Traktujemy ją jak brak zalogowania,
  // a SessionExpiryWatcher ją czyści.
  // Odświeżanie tokenu (update()) przełącza status na "loading", ale sesja dalej jest. Gdyby to
  // liczyło się jako wylogowanie, AuthSync czyściłby role i na chwilę znikały wszystkie przyciski.
  const isAuthenticated = status !== "unauthenticated" && !session?.error && !!session?.accessToken
  return { request, getToken, isAuthenticated }
}
