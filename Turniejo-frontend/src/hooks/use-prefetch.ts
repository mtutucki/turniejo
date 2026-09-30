"use client"

import { type QueryKey, useQueryClient } from "@tanstack/react-query"
import { useCallback } from "react"

import { useApi } from "@/hooks/use-api"

type PrefetchTarget = { queryKey: QueryKey; path: string }

/**
 * Prefetch danych strony docelowej przy najechaniu/fokusie/dotyku linku — zanim user kliknie, lista
 * albo panel są już w cache, więc strona od razu pokazuje treść zamiast skeletonu. Świeże dane
 * (staleTime) nie są pobierane ponownie.
 */
export function usePrefetch() {
  const queryClient = useQueryClient()
  const { request, isAuthenticated } = useApi()

  return useCallback(
    (...targets: PrefetchTarget[]) => {
      if (!isAuthenticated) return
      for (const { queryKey, path } of targets) {
        void queryClient.prefetchQuery({
          queryKey,
          queryFn: () => request(path),
          staleTime: 30_000,
        })
      }
    },
    [queryClient, request, isAuthenticated],
  )
}

/** Handlery dla <Link>: mysz, klawiatura i pierwszy dotyk na telefonie. */
export const prefetchHandlers = (prefetch: () => void) => ({
  onMouseEnter: prefetch,
  onFocus: prefetch,
  onTouchStart: prefetch,
})
