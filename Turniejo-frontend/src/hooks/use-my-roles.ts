"use client"

import { useCallback, useMemo } from "react"

import { selectRoles, useAuthStore } from "@/stores/auth-store"
import type { ResourceType, UserRole } from "@/types/roles"

/** Invaliduj po każdej zmianie ról (np. dołączenie do turnieju kodem) — AuthSync odświeży store. */
export const myRolesQueryKey = ["me", "roles"] as const

type UseMyRolesArgs = {
  resourceType: ResourceType
  resourceId: string | undefined
}

/** Role zalogowanego usera w konkretnym zasobie (z cache w useAuthStore). */
export function useMyRoles({ resourceType, resourceId }: UseMyRolesArgs) {
  const assignments = useAuthStore((state) => state.roleAssignments)

  const roles = useMemo(
    () => selectRoles(assignments, resourceType, resourceId),
    [assignments, resourceType, resourceId],
  )
  const hasRole = useCallback((role: UserRole) => roles.includes(role), [roles])
  const hasAnyRole = useCallback(
    (allowed: readonly UserRole[]) => allowed.some((role) => roles.includes(role)),
    [roles],
  )

  return { roles, hasRole, hasAnyRole, isLoading: assignments === null }
}
