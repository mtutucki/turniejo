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

  return { request, isAuthenticated: status === "authenticated" }
}
