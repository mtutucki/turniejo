// Kontrakty modułów Teams i Players. Jak w tournaments.ts: DTO zwracają nazwy enumów,
// body requestów przyjmuje wartości liczbowe.

// 1:1 z Turniejo.Teams.Domain.Enums.RegistrationStatus
export const RegistrationStatus = { Pending: 1, Accepted: 2, Rejected: 3, Withdrawn: 4 } as const
export type RegistrationStatus = keyof typeof RegistrationStatus

export const REGISTRATION_STATUS_LABELS: Record<RegistrationStatus, string> = {
  Pending: "Oczekuje",
  Accepted: "Zaakceptowana",
  Rejected: "Odrzucona",
  Withdrawn: "Wycofana",
}

/** Lustro TournamentTeam.AllowedTransitions w domenie backendu. */
export const REGISTRATION_TRANSITIONS: Record<RegistrationStatus, RegistrationStatus[]> = {
  Pending: ["Accepted", "Rejected"],
  Accepted: ["Withdrawn"],
  Rejected: [],
  Withdrawn: [],
}

// 1:1 z Turniejo.Teams.Domain.Enums.PaymentStatus
export const PaymentStatus = { NotRequired: 1, Unpaid: 2, Paid: 3, Refunded: 4 } as const
export type PaymentStatus = keyof typeof PaymentStatus

export const PAYMENT_STATUS_LABELS: Record<PaymentStatus, string> = {
  NotRequired: "Nie dotyczy",
  Unpaid: "Nieopłacona",
  Paid: "Opłacona",
  Refunded: "Zwrócona",
}

// 1:1 z Turniejo.Teams.Domain.Enums.TeamStaffRole
export const TeamStaffRole = { Coach: 1, AssistantCoach: 2, Manager: 3 } as const
export type TeamStaffRole = keyof typeof TeamStaffRole

export const TEAM_STAFF_ROLE_LABELS: Record<TeamStaffRole, string> = {
  Coach: "Trener",
  AssistantCoach: "Asystent trenera",
  Manager: "Kierownik",
}

// 1:1 z Turniejo.Players.Domain.Enums.PlayerPosition
export const PlayerPosition = {
  Goalkeeper: 1,
  Defender: 2,
  Midfielder: 3,
  Forward: 4,
  Universal: 5,
  Unknown: 6,
} as const
export type PlayerPosition = keyof typeof PlayerPosition

export const PLAYER_POSITION_LABELS: Record<PlayerPosition, string> = {
  Goalkeeper: "Bramkarz",
  Defender: "Obrońca",
  Midfielder: "Pomocnik",
  Forward: "Napastnik",
  Universal: "Uniwersalny",
  Unknown: "Nieokreślona",
}

// 1:1 z Turniejo.Players.Domain.Enums.TournamentPlayerStatus
export type TournamentPlayerStatus = "Active" | "Injured" | "Suspended" | "Removed"

export const PLAYER_STATUS_LABELS: Record<TournamentPlayerStatus, string> = {
  Active: "Aktywny",
  Injured: "Kontuzjowany",
  Suspended: "Zawieszony",
  Removed: "Usunięty",
}

/** Role nadawane przez /join (JoinTournamentCommandHandler). */
export const JOIN_ROLE_LABELS: Record<string, string> = {
  Referee: "Sędzia",
  Coach: "Trener",
  Viewer: "Kibic / rodzic",
}

/** TeamDto — globalna drużyna organizacji. */
export type Team = {
  id: string
  organizationId: string
  name: string
  shortName: string | null
  logoUrl: string | null
}

/** TournamentTeamDto — zgłoszenie drużyny do turnieju. */
export type TournamentTeam = {
  id: string
  tournamentId: string
  teamId: string
  categoryId: string
  groupId: string | null
  registrationStatus: RegistrationStatus
  paymentStatus: PaymentStatus
  seedNumber: number | null
  teamName: string
  teamShortName: string | null
  teamLogoUrl: string | null
}

/** TournamentPlayerDto — zawodnik w składzie drużyny turniejowej. */
export type TournamentPlayer = {
  id: string
  tournamentTeamId: string
  playerId: string
  shirtNumber: number | null
  position: PlayerPosition | null
  status: TournamentPlayerStatus
  firstName: string
  lastName: string
  birthDate: string | null
  photoUrl: string | null
}

/** TeamStaffDto */
export type TeamStaffMember = {
  id: string
  tournamentTeamId: string
  userId: string
  role: TeamStaffRole
  email: string | null
  firstName: string | null
  lastName: string | null
}

/** TournamentMemberDto — osoba z rolą w turnieju (GET /members, tylko organizator). */
export type TournamentMember = {
  userId: string
  email: string
  firstName: string
  lastName: string
  roles: string[]
}

/** JoinTournamentResponse */
export type JoinResult = {
  tournamentId: string
  tournamentName: string
  slug: string
  role: string
  isPublic: boolean
}

export type TeamInput = {
  name: string
  shortName: string | null
  logoUrl: string | null
}

export type NewPlayerInput = {
  firstName: string
  lastName: string
  /** ISO z północą UTC — kolumna BirthDate to timestamptz. */
  birthDate: string
  position: PlayerPosition
  shirtNumber: number | null
  photoUrl: string | null
  /** Potwierdzone w formularzu: ktoś w składzie ma te same dane, ale to inna osoba. */
  allowDuplicate?: boolean
}

/** Pełna edycja zawodnika w składzie: profil + numer/pozycja/status (PUT …/players/{id}). */
export type SquadPlayerInput = NewPlayerInput & { status: TournamentPlayerStatus }

/** RosterSourcePlayerDto — flagi liczone względem drużyny, do której kopiujemy. */
export type RosterSourcePlayer = {
  playerId: string
  firstName: string
  lastName: string
  birthDate: string
  shirtNumber: number | null
  position: PlayerPosition | null
  alreadyInSquad: boolean
  ineligibility: "TooOld" | "TooYoung" | null
}

/**
 * RosterSourceDto — skąd wziąć zawodników do składu: kadra drużyny (kind "Roster", bez turnieju)
 * albo skład tej samej drużyny z innego turnieju (kind "Tournament").
 */
export type RosterSource = {
  kind: "Roster" | "Tournament"
  tournamentTeamId: string | null
  tournamentId: string | null
  tournamentName: string
  tournamentStartAt: string | null
  categoryName: string | null
  players: RosterSourcePlayer[]
}

export type ImportSquadInput = {
  /** null = kadra drużyny. */
  sourceTournamentTeamId: string | null
  players: { playerId: string; shirtNumber: number | null; position: PlayerPosition | null }[]
}

// --- Kadra drużyny -------------------------------------------------------------------------

/** TeamRosterSquadDto — skład w turnieju, który trwa albo się nie zaczął. */
export type TeamRosterSquad = {
  tournamentTeamId: string
  tournamentId: string
  tournamentName: string
  tournamentStatus: string
  tournamentStartAt: string | null
  /** Czy bieżący użytkownik może zmienić ten skład (np. blokada po starcie turnieju). */
  canEditSquad: boolean
}

/** TeamRosterPlayerDto */
export type TeamRosterPlayer = {
  playerId: string
  firstName: string
  lastName: string
  birthDate: string
  defaultPosition: PlayerPosition
  photoUrl: string | null
  shirtNumber: number | null
  status: "Active" | "Left"
  leftAt: string | null
  currentSquads: TeamRosterSquad[]
  pastTournamentsCount: number
}

/** TeamRosterDto — GET /api/teams/{teamId}/players */
export type TeamRoster = {
  teamId: string
  teamName: string
  canEdit: boolean
  players: TeamRosterPlayer[]
}

export type RosterPlayerInput = {
  firstName: string
  lastName: string
  /** ISO z północą UTC. */
  birthDate: string
  position: PlayerPosition
  shirtNumber: number | null
  allowDuplicate?: boolean
}

/** RemoveTeamPlayerResponse */
export type RemoveRosterPlayerResult = {
  playerId: string
  /** true: profil usunięty całkiem (nigdzie nie grał); false: został jako były zawodnik. */
  deleted: boolean
  removedFromSquads: number
  keptInSquads: string[]
}

/** TeamRecordDto — bilans z zakończonych meczów. */
export type TeamRecordSummary = {
  played: number
  wins: number
  draws: number
  losses: number
  goalsFor: number
  goalsAgainst: number
}

/** Wynik drużyny w turnieju (TeamHistoryTournamentDto.ResultKind). */
export type TeamHistoryResultKind =
  "Champion" | "RunnerUp" | "Third" | "Fourth" | "Eliminated" | "InCup" | "Group" | "None"

/** TeamHistoryTournamentDto — jedno zgłoszenie (turniej + kategoria). */
export type TeamHistoryTournament = {
  tournamentId: string
  tournamentName: string
  tournamentStatus: string
  startAt: string | null
  endAt: string | null
  tournamentTeamId: string
  categoryName: string | null
  resultKind: TeamHistoryResultKind
  /** 2 = półfinał, 3 = ćwierćfinał… (przy Eliminated). */
  eliminatedRoundFromEnd: number | null
  groupPosition: number | null
  groupName: string | null
  record: TeamRecordSummary
}

/** TeamHistoryDto — GET /api/teams/{teamId}/history: drużyna z bazy we wszystkich turniejach. */
export type TeamHistory = {
  summary: TeamRecordSummary
  tournamentCount: number
  /** Ostatnie wyniki, najstarszy pierwszy. */
  form: ("W" | "D" | "L")[]
  tournaments: TeamHistoryTournament[]
  players: {
    playerId: string
    firstName: string
    lastName: string
    goals: number
    assists: number
    mvp: number
    yellowCards: number
    redCards: number
  }[]
  opponents: { teamId: string; name: string; record: TeamRecordSummary }[]
}
