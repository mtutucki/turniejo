// Enumy z Turniejo.Matches.Domain — DTO zwracają nazwy (string).

export const MatchStatus = {
  Scheduled: 1,
  Ready: 2,
  Live: 3,
  Paused: 4,
  Finished: 5,
  Postponed: 6,
  Cancelled: 7,
} as const
export type MatchStatus = keyof typeof MatchStatus

export const MATCH_STATUS_LABELS: Record<MatchStatus, string> = {
  Scheduled: "Zaplanowany",
  Ready: "Gotowy",
  Live: "Na żywo",
  Paused: "Przerwa",
  Finished: "Zakończony",
  Postponed: "Przełożony",
  Cancelled: "Odwołany",
}

/** Termin, boisko i sędziego da się zmienić tylko przed rozpoczęciem (Match.IsSchedulable w domenie). */
export const SCHEDULABLE_STATUSES: MatchStatus[] = ["Scheduled", "Ready", "Postponed"]

export type StageType = "Group" | "Knockout" | "ThirdPlace" | "Final" | "Friendly"

/** PitchDto */
export type Pitch = {
  id: string
  tournamentId: string
  name: string
  description: string | null
  isActive: boolean
}

export type PitchInput = {
  name: string
  description: string | null
  isActive: boolean
}

/** MatchDto (GET /schedule) */
export type Match = {
  id: string
  tournamentId: string
  categoryId: string | null
  groupId: string | null
  pitchId: string | null
  homeTournamentTeamId: string
  awayTournamentTeamId: string
  refereeId: string | null
  scheduledAt: string
  status: MatchStatus
  stageType: StageType
  homeScore: number
  awayScore: number
  resultType: string
  homePenaltyScore: number | null
  awayPenaltyScore: number | null
}

export type GenerateScheduleInput = {
  startAt: string
  endAt: string
  pitchIds: string[]
}

// --- Panel live (Faza 5) -------------------------------------------------------------------

// 1:1 z Turniejo.Matches.Domain.Enums.MatchEventType — request przyjmuje liczby, DTO zwraca nazwy.
export const MatchEventType = {
  MatchStarted: 1,
  MatchPaused: 2,
  MatchResumed: 3,
  MatchFinished: 4,
  Goal: 5,
  OwnGoal: 6,
  PenaltyGoal: 7,
  YellowCard: 8,
  RedCard: 9,
  Assist: 10,
  MvpSelected: 11,
} as const
export type MatchEventType = keyof typeof MatchEventType

export type GoalType = "Goal" | "OwnGoal" | "PenaltyGoal"
export type CardType = "YellowCard" | "RedCard"

export const GOAL_TYPE_LABELS: Record<GoalType, string> = {
  Goal: "Gol",
  PenaltyGoal: "Rzut karny",
  OwnGoal: "Samobój",
}

export const EVENT_LABELS: Partial<Record<MatchEventType, string>> = {
  MatchStarted: "Początek meczu",
  MatchPaused: "Przerwa",
  MatchResumed: "Wznowienie",
  MatchFinished: "Koniec meczu",
  Goal: "Gol",
  OwnGoal: "Gol samobójczy",
  PenaltyGoal: "Gol z karnego",
  YellowCard: "Żółta kartka",
  RedCard: "Czerwona kartka",
  Assist: "Asysta",
  MvpSelected: "MVP meczu",
}

export const GOAL_TYPES: GoalType[] = ["Goal", "PenaltyGoal", "OwnGoal"]
export const isGoal = (type: string): type is GoalType =>
  type === "Goal" || type === "OwnGoal" || type === "PenaltyGoal"

// 1:1 z Turniejo.Matches.Domain.Enums.MatchResultType
export const MatchResultType = {
  NotPlayed: 1,
  Regular: 2,
  ExtraTime: 3,
  Penalties: 4,
  Walkover: 5,
  ManualDecision: 6,
} as const
export type MatchResultType = keyof typeof MatchResultType

/** MatchEventDto */
export type MatchEvent = {
  id: string
  matchId: string
  type: MatchEventType
  tournamentTeamId: string | null
  playerId: string | null
  relatedPlayerId: string | null
  minute: number
  second: number | null
  createdByUserId: string
  occurredAt: string
}

/** MatchPlayerDto */
export type MatchPlayer = {
  tournamentPlayerId: string
  playerId: string
  firstName: string
  lastName: string
  shirtNumber: number | null
  position: string | null
  status: string
}

/** MatchTeamDto */
export type MatchTeam = {
  tournamentTeamId: string
  teamId: string
  name: string
  shortName: string | null
  players: MatchPlayer[]
}

/** MatchLivePanelDto — też "snapshot" pobierany po (ponownym) połączeniu z SignalR. */
export type MatchLivePanel = {
  matchId: string
  tournamentId: string
  status: MatchStatus
  stageType: StageType
  scheduledAt: string
  startedAt: string | null
  pausedAt: string | null
  finishedAt: string | null
  totalPausedSeconds: number
  homeScore: number
  awayScore: number
  resultType: MatchResultType
  winnerTournamentTeamId: string | null
  homeTeam: MatchTeam
  awayTeam: MatchTeam
  events: MatchEvent[]
  /** Do kiedy po zakończeniu sędzia może poprawiać zdarzenia. */
  editLockedAt: string | null
  /** Sekundy gry policzone przez serwer w chwili odpowiedzi. */
  elapsedSeconds: number | null
  homePenaltyScore: number | null
  awayPenaltyScore: number | null
}

/** MatchClockDto */
export type MatchClock = { matchId: string; status: MatchStatus; elapsedSeconds: number }

/** MatchLocationDto */
export type MatchLocation = {
  matchId: string
  tournamentId: string
  scheduledAt: string
  pitchId: string | null
  pitchName: string | null
  pitchDescription: string | null
}

export type AddGoalInput = {
  tournamentTeamId: string
  playerId: string | null
  assistPlayerId: string | null
  goalType: GoalType
  minute: number
}

export type AddCardInput = {
  tournamentTeamId: string
  playerId: string
  cardType: CardType
  minute: number
}
