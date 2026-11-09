import type { MatchStatus } from "./matches"
import type { PlayerPosition } from "./teams"
import type { TournamentStatus } from "./tournaments"

// --- Panel rodzica -------------------------------------------------------------------------

/** MyChildDto */
export type MyChild = {
  playerId: string
  firstName: string
  lastName: string
  birthDate: string
  defaultPosition: PlayerPosition
  photoUrl: string | null
  relationshipType: "Parent" | "Guardian" | "Other"
  canEdit: boolean
}

/** MyChildTeamDto */
export type MyChildTeam = {
  tournamentPlayerId: string
  tournamentTeamId: string
  tournamentId: string
  tournamentName: string
  teamId: string
  teamName: string
  groupId: string | null
  shirtNumber: number | null
  position: PlayerPosition | null
}

/** MyChildMatchDto */
export type MyChildMatch = {
  matchId: string
  tournamentId: string
  tournamentName: string
  scheduledAt: string
  status: MatchStatus
  homeTeamName: string
  awayTeamName: string
  homeScore: number
  awayScore: number
}

/** GroupStandingDto */
export type GroupStandingRow = {
  tournamentTeamId: string
  position: number
  points: number
  matchesPlayed: number
  wins: number
  draws: number
  losses: number
  goalsFor: number
  goalsAgainst: number
}

/** MyChildStandingDto */
export type MyChildStanding = {
  tournamentTeamId: string
  groupId: string
  standings: GroupStandingRow[]
  groupName: string | null
  teamNames: Record<string, string> | null
  tournamentName: string | null
}

export const RELATIONSHIP_LABELS: Record<MyChild["relationshipType"], string> = {
  Parent: "Rodzic",
  Guardian: "Opiekun prawny",
  Other: "Opiekun",
}

// --- Panel trenera -------------------------------------------------------------------------

/** MyTeamDto */
export type MyTeam = {
  tournamentTeamId: string
  tournamentId: string
  tournamentName: string
  teamId: string
  teamName: string
  categoryId: string
  groupId: string | null
  role: "Coach" | "AssistantCoach" | "Manager"
  tournamentStatus: TournamentStatus
  /** Liczone na backendzie tak jak SquadEditGuard (status turnieju + CanCoachEditSquadAfterStart). */
  canEditSquad: boolean
}

/** TeamMatchDto */
export type TeamMatch = {
  matchId: string
  scheduledAt: string
  status: MatchStatus
  homeTeamName: string
  awayTeamName: string
  homeScore: number
  awayScore: number
}

/** TeamPlayerStatsDto */
export type TeamPlayerStats = {
  tournamentPlayerId: string
  playerId: string
  firstName: string
  lastName: string
  goals: number
  assists: number
  yellowCards: number
  redCards: number
}

// 1:1 z Turniejo.Players.Domain.Enums.TournamentPlayerStatus
/** Wartości liczbowe enuma do body PUT …/players/{id} (typ: TournamentPlayerStatus z types/teams). */
export const TOURNAMENT_PLAYER_STATUS_VALUES = {
  Active: 1,
  Injured: 2,
  Suspended: 3,
  Removed: 4,
} as const

// --- Powiadomienia -------------------------------------------------------------------------

export type NotificationType =
  | "MatchStarted"
  | "MatchFinished"
  | "MatchTimeChanged"
  | "PitchChanged"
  | "OrganizerAnnouncement"
  | "MatchReminder"
  | "ScheduleChanged"
  | "TournamentCancelled"
  | "RefereeChanged"

/** NotificationDto */
export type AppNotification = {
  notificationRecipientId: string
  notificationId: string
  tournamentId: string | null
  matchId: string | null
  type: NotificationType
  title: string
  message: string
  status: "Pending" | "Delivered" | "Read"
  createdAt: string
  readAt: string | null
}

export const NOTIFICATION_TYPE_LABELS: Record<NotificationType, string> = {
  MatchStarted: "Mecz się rozpoczął",
  MatchFinished: "Koniec meczu",
  MatchTimeChanged: "Zmiana godziny",
  PitchChanged: "Zmiana boiska",
  OrganizerAnnouncement: "Ogłoszenie",
  MatchReminder: "Przypomnienie",
  ScheduleChanged: "Zmiana terminarza",
  TournamentCancelled: "Turniej odwołany",
  RefereeChanged: "Zmiana sędziego",
}
