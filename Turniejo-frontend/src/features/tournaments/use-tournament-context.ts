"use client"

import { useParams } from "next/navigation"

import { useMyRoles } from "@/hooks/use-my-roles"
import { UserRole } from "@/types/roles"

import { useTournament } from "./api"

/** Turniej z adresu /tournaments/[id]/… oraz to, czy zalogowany user nim zarządza. */
export function useTournamentContext() {
  const { id } = useParams<{ id: string }>()
  const tournament = useTournament(id)
  const { hasRole, isLoading: rolesLoading } = useMyRoles({
    resourceType: "Tournament",
    resourceId: id,
  })

  return {
    id,
    tournament,
    isOrganizer: hasRole(UserRole.Organizer),
    rolesLoading,
  }
}
