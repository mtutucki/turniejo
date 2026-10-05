"use client"

import { useQuery } from "@tanstack/react-query"
import { useEffect } from "react"

import { useApi } from "@/hooks/use-api"
import { myRolesQueryKey } from "@/hooks/use-my-roles"
import { useAuthStore } from "@/stores/auth-store"
import type { RoleAssignment } from "@/types/roles"

/** Pobiera role zalogowanego usera do useAuthStore i czyści je po wylogowaniu. */
export function AuthSync() {
  const { request, isAuthenticated } = useApi()
  const setRoleAssignments = useAuthStore((state) => state.setRoleAssignments)
  const reset = useAuthStore((state) => state.reset)

  const { data } = useQuery({
    queryKey: myRolesQueryKey,
    queryFn: () => request<RoleAssignment[]>("/api/me/roles"),
    enabled: isAuthenticated,
    // Role zmieniają się w trakcie sesji (dołączenie kodem, przypisanie sędziego do meczu przez
    // organizatora) — krótki staleTime + odświeżanie przy powrocie do karty.
    staleTime: 30_000,
    refetchOnWindowFocus: true,
    // Rola w osobistej organizacji powstaje asynchronicznie (outbox backendu, co 10 s) —
    // świeżo zarejestrowany user dostałby pustą listę na 5 minut. Dopytujemy przez ~30 s.
    refetchInterval: (query) =>
      query.state.data?.length === 0 && query.state.dataUpdateCount < 8 ? 4_000 : false,
  })

  useEffect(() => {
    if (!isAuthenticated) reset()
    else if (data) setRoleAssignments(data)
  }, [isAuthenticated, data, reset, setRoleAssignments])

  return null
}
