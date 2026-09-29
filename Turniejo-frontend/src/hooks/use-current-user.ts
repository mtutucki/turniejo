"use client"

import { useQuery } from "@tanstack/react-query"

import { useApi } from "./use-api"

export type CurrentUser = {
  userId: string
  email: string
  isAuthenticated: boolean
}

export const currentUserQueryKey = ["me"] as const

export function useCurrentUser() {
  const { request, isAuthenticated } = useApi()

  return useQuery({
    queryKey: currentUserQueryKey,
    queryFn: () => request<CurrentUser>("/api/me"),
    enabled: isAuthenticated,
    staleTime: Infinity,
  })
}
