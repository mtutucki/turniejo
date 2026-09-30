"use client"

import { worksInTournament } from "@/lib/auth/role-landing"
import { useAuthStore } from "@/stores/auth-store"

import { GLOBAL_NAV_ITEMS } from "./nav-items"

/** Globalne pozycje nawigacji odfiltrowane po rolach zalogowanego usera (panel rodzica/trenera). */
export function useGlobalNavItems() {
  const assignments = useAuthStore((state) => state.roleAssignments)
  return GLOBAL_NAV_ITEMS.filter((item) => !item.requires || item.requires(assignments ?? []))
}

/**
 * Czy pokazywać sekcję aktywnego turnieju (Mecze/Tabela/Drużyny). Rodzic czy kibic jej nie
 * potrzebuje. Dopóki role się wczytują — tak, żeby organizatorowi nie migał układ rodzica.
 */
export function useShowsTournamentSection() {
  const assignments = useAuthStore((state) => state.roleAssignments)
  return assignments === null || worksInTournament(assignments)
}
