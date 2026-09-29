"use client"

import type { ReactNode } from "react"

import { useMyRoles } from "@/hooks/use-my-roles"
import type { ResourceType, UserRole } from "@/types/roles"

type RoleGateProps = {
  resourceType: ResourceType
  resourceId: string | undefined
  allow: readonly UserRole[]
  /** Renderowane, gdy user nie ma żadnej z ról (domyślnie nic). */
  fallback?: ReactNode
  children: ReactNode
}

/**
 * Ukrywa elementy UI, do których user nie ma roli w danym zasobie.
 * To tylko UX — właściwą autoryzację i tak robi backend (403).
 */
export function RoleGate({
  resourceType,
  resourceId,
  allow,
  fallback = null,
  children,
}: RoleGateProps) {
  const { hasAnyRole, isLoading } = useMyRoles({ resourceType, resourceId })

  if (isLoading || !hasAnyRole(allow)) return fallback
  return children
}
