"use client"

import { usePitches, useSchedule } from "@/features/matches/api"
import { useTournamentTeams } from "@/features/teams/api"
import { statusAtLeast, type Tournament } from "@/types/tournaments"

import { useCategories } from "./api"
import { SETUP_CHECKLIST, type SetupChecklistKey } from "./setup-steps"

/**
 * Co już jest gotowe do rozegrania turnieju — liczone z danych, nie z tego, które ekrany organizator
 * odwiedził. Dzięki temu kreator i przegląd pokazują to samo, także po powrocie za tydzień.
 */
export function useSetupProgress(id: string, tournament: Tournament | undefined) {
  const categories = useCategories(id)
  const pitches = usePitches(id)
  const teams = useTournamentTeams(id)
  const schedule = useSchedule(id)

  const activePitches = pitches.data?.filter((p) => p.isActive).length ?? 0
  const acceptedTeams = teams.data?.filter((t) => t.registrationStatus === "Accepted") ?? []
  const pendingTeams = teams.data?.filter((t) => t.registrationStatus === "Pending") ?? []

  const done: Record<SetupChecklistKey, boolean> = {
    format: !!tournament?.format,
    categories: (categories.data?.length ?? 0) > 0,
    pitches: activePitches > 0,
    // Dwie drużyny to minimum, żeby dało się ułożyć choć jeden mecz.
    teams: acceptedTeams.length >= 2,
    schedule: !!tournament && statusAtLeast(tournament.status, "ScheduleGenerated"),
  }

  return {
    isPending: categories.isPending || pitches.isPending || teams.isPending,
    done,
    doneCount: SETUP_CHECKLIST.filter((key) => done[key]).length,
    /** Pierwszy krok, w którym czegoś brakuje — tu kreator wznawia pracę. */
    firstIncomplete: SETUP_CHECKLIST.find((key) => !done[key]) ?? "schedule",
    categoryCount: categories.data?.length ?? 0,
    activePitches,
    acceptedTeams,
    pendingTeams,
    /** Puchar: mecze 1. rundy istnieją od utworzenia drabinki, jeszcze przed terminarzem. */
    matchCount: schedule.data?.length ?? 0,
  }
}
