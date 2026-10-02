// Kontrakty modułu Tournaments. Backend serializuje enumy w DTO jako nazwy ("Draft"),
// ale w body requestów przyjmuje wartości liczbowe — stąd mapy nazwa → liczba.

// 1:1 z Turniejo.Tournaments.Domain.Enums.TournamentStatus
export const TournamentStatus = {
  Draft: 1,
  RegistrationOpen: 2,
  RegistrationClosed: 3,
  ScheduleGenerated: 4,
  InProgress: 5,
  Finished: 6,
  Cancelled: 7,
} as const
export type TournamentStatus = keyof typeof TournamentStatus

export const TOURNAMENT_STATUS_LABELS: Record<TournamentStatus, string> = {
  Draft: "Szkic",
  RegistrationOpen: "Zapisy otwarte",
  RegistrationClosed: "Zapisy zamknięte",
  ScheduleGenerated: "Harmonogram gotowy",
  InProgress: "W trakcie",
  Finished: "Zakończony",
  Cancelled: "Anulowany",
}

/** Kolejne etapy "szczęśliwej ścieżki" — Cancelled jest poza nią. */
export const TOURNAMENT_STATUS_FLOW: TournamentStatus[] = [
  "Draft",
  "RegistrationOpen",
  "RegistrationClosed",
  "ScheduleGenerated",
  "InProgress",
  "Finished",
]

/** Dozwolone przejścia — lustro Tournament.ChangeStatus w domenie backendu. */
export const TOURNAMENT_STATUS_TRANSITIONS: Record<TournamentStatus, TournamentStatus[]> = {
  Draft: ["RegistrationOpen", "Cancelled"],
  RegistrationOpen: ["RegistrationClosed", "Cancelled"],
  RegistrationClosed: ["ScheduleGenerated", "Cancelled"],
  ScheduleGenerated: ["InProgress", "Cancelled"],
  InProgress: ["Finished", "Cancelled"],
  Finished: [],
  Cancelled: [],
}

/**
 * Cofnięcie o jeden etap. Draft/RegistrationOpen to zwykła zmiana statusu (Tournament.ChangeStatus),
 * RegistrationClosed z ScheduleGenerated wymaga wyczyszczenia terminarza (DELETE schedule).
 */
export const TOURNAMENT_STATUS_REVERT: Partial<Record<TournamentStatus, TournamentStatus>> = {
  RegistrationOpen: "Draft",
  RegistrationClosed: "RegistrationOpen",
  ScheduleGenerated: "RegistrationClosed",
}

export function statusAtLeast(status: TournamentStatus, threshold: TournamentStatus) {
  return TournamentStatus[status] >= TournamentStatus[threshold]
}

// 1:1 z Turniejo.Tournaments.Domain.Enums.TournamentFormat
export const TournamentFormat = {
  Group: 1,
  Knockout: 2,
  GroupAndKnockout: 3,
} as const
export type TournamentFormat = keyof typeof TournamentFormat

export const TOURNAMENT_FORMAT_LABELS: Record<TournamentFormat, string> = {
  Group: "Grupowy",
  Knockout: "Pucharowy",
  GroupAndKnockout: "Grupy + puchar",
}

// 1:1 z Turniejo.Tournaments.Domain.Enums.TieBreakerType
export const TieBreakerType = {
  HeadToHeadPoints: 1,
  HeadToHeadGoalDiff: 2,
  GoalDifference: 3,
  GoalsFor: 4,
  Wins: 5,
  ManualDecision: 6,
  RandomDraw: 7,
} as const
export type TieBreakerType = keyof typeof TieBreakerType

export const TIE_BREAKER_LABELS: Record<TieBreakerType, string> = {
  HeadToHeadPoints: "Punkty w meczach bezpośrednich",
  HeadToHeadGoalDiff: "Bilans bramek w meczach bezpośrednich",
  GoalDifference: "Bilans bramek",
  GoalsFor: "Bramki zdobyte",
  Wins: "Liczba zwycięstw",
  ManualDecision: "Decyzja organizatora",
  RandomDraw: "Losowanie",
}

/** GET /api/organizations/mine */
export type MyOrganization = {
  id: string
  name: string
  slug: string
}

/** TournamentDto */
export type Tournament = {
  id: string
  name: string
  slug: string
  status: TournamentStatus
  format: TournamentFormat | null
  startAt: string
  endAt: string
  teamLimit: number
  isPaid: boolean
  // Backend zwraca kody tylko organizatorowi turnieju — pozostali członkowie dostają null.
  refereeCode: string | null
  coachCode: string | null
  viewerCode: string | null
  description: string | null
  // Portal kibica (/t/{slug}) włączony — inaczej link prowadzi na 404.
  isPublic: boolean
}

/** TournamentDashboardDto */
export type TournamentDashboard = {
  tournamentId: string
  tournamentName: string
  tournamentStatus: TournamentStatus
  teams: { pending: number; accepted: number; rejected: number; withdrawn: number; total: number }
  matches: { finished: number; scheduled: number; total: number }
  scheduleStatus: "NotGenerated" | "Generated" | "InProgress" | "Completed"
  upcomingMatches: {
    matchId: string
    scheduledAt: string
    status: string
    homeTeamName: string
    awayTeamName: string
  }[]
}

/** TournamentSettingsDto (bez tournamentId) — też body PUT /settings. */
export type TournamentSettings = {
  pointsForWin: number
  pointsForDraw: number
  pointsForLoss: number
  matchDurationMinutes: number
  breakDurationMinutes: number
  isThirdPlaceMatchEnabled: boolean
  refereeEditWindowMinutes: number
  walkoverHomeScore: number
  walkoverAwayScore: number
  canCoachEditSquadAfterStart: boolean
}

/** TournamentCategoryDto */
export type TournamentCategory = {
  id: string
  tournamentId: string
  name: string
  minBirthDate: string | null
  maxBirthDate: string | null
  order: number
}

/** TournamentGroupDto */
export type TournamentGroup = {
  id: string
  tournamentId: string
  categoryId: string
  name: string
  order: number
}

/** TieBreakerRuleDto */
export type TieBreakerRule = {
  order: number
  type: TieBreakerType
}

export type TournamentDetailsInput = {
  name: string
  description: string | null
  startAt: string
  endAt: string
  teamLimit: number
}

export type CategoryInput = {
  name: string
  minBirthDate: string | null
  maxBirthDate: string | null
  order: number
}
