import { create } from "zustand"

import type { ResourceType, RoleAssignment, UserRole } from "@/types/roles"

type AuthState = {
  /** null = jeszcze nie pobrane z API. */
  roleAssignments: RoleAssignment[] | null
  setRoleAssignments: (assignments: RoleAssignment[]) => void
  reset: () => void
}

/**
 * Cache uprawnień zalogowanego usera. Wypełnia go <AuthSync> (GET /api/me/roles),
 * czyści wylogowanie. Czytaj przez useMyRoles, nie bezpośrednio.
 */
export const useAuthStore = create<AuthState>()((set) => ({
  roleAssignments: null,
  setRoleAssignments: (roleAssignments) => set({ roleAssignments }),
  reset: () => set({ roleAssignments: null }),
}))

/**
 * Role w konkretnym zasobie. Dopasowanie jest dokładne (typ + id), tak jak
 * w AuthorizationBehavior backendu — Organizer organizacji nie jest automatycznie
 * Organizerem jej turniejów.
 */
export function selectRoles(
  assignments: RoleAssignment[] | null,
  resourceType: ResourceType,
  resourceId: string | undefined,
): UserRole[] {
  if (!assignments || !resourceId) return []
  const id = resourceId.toLowerCase()
  return assignments
    .filter((a) => a.scopeType === resourceType && a.scopeId?.toLowerCase() === id)
    .map((a) => a.role)
}
