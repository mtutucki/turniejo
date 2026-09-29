"use client"

import { useQuery } from "@tanstack/react-query"
import { useCallback, useMemo } from "react"

import type { ResourceType, RoleAssignment, UserRole } from "@/types/roles"

import { useApi } from "./use-api"

/** Invaliduj po każdej zmianie ról (np. dołączenie do turnieju kodem). */
export const myRolesQueryKey = ["me", "roles"] as const

/** Wszystkie przypisania ról zalogowanego usera (rola + scope). */
export function useMyRoleAssignments() {
  const { request, isAuthenticated } = useApi()

  return useQuery({
    queryKey: myRolesQueryKey,
    queryFn: () => request<RoleAssignment[]>("/api/me/roles"),
    enabled: isAuthenticated,
    staleTime: 5 * 60_000,
  })
}

type UseMyRolesArgs = {
  resourceType: ResourceType
  resourceId: string | undefined
}

/**
 * Role zalogowanego usera w konkretnym zasobie. Dopasowanie jest dokładne (typ + id),
 * tak jak w AuthorizationBehavior backendu — Organizer organizacji nie jest automatycznie
 * Organizerem jej turniejów.
 */
export function useMyRoles({ resourceType, resourceId }: UseMyRolesArgs) {
  const { data, isLoading } = useMyRoleAssignments()

  const roles = useMemo(() => {
    if (!data || !resourceId) return []
    const id = resourceId.toLowerCase()
    return data
      .filter((a) => a.scopeType === resourceType && a.scopeId?.toLowerCase() === id)
      .map((a) => a.role)
  }, [data, resourceType, resourceId])

  const hasRole = useCallback((role: UserRole) => roles.includes(role), [roles])
  const hasAnyRole = useCallback(
    (allowed: readonly UserRole[]) => allowed.some((role) => roles.includes(role)),
    [roles],
  )

  return { roles, hasRole, hasAnyRole, isLoading }
}
