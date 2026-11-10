import type { Match } from "./matches"
import type { TournamentFormat, TournamentStatus } from "./tournaments"

/** Widoczność sekcji portalu (PublicTournamentSettingsDto). */
export type PublicVisibility = {
  showTeams: boolean
  showPlayers: boolean
  showSchedule: boolean
  showStandings: boolean
  showBracket: boolean
  showLiveMatches: boolean
  showStats: boolean
}

/** TournamentPublicSettingsDto — formularz organizatora (GET/PUT /public-settings). */
export type PublicSettings = PublicVisibility & {
  isPublic: boolean
  /** RODO: portal pokazuje "Jan K." zamiast pełnego nazwiska (domyślnie włączone). */
  abbreviatePlayerNames: boolean
  /** Turniej widać na liście turniejów publicznych; wyłączony: portal tylko przez link. */
  showInDirectory: boolean
}

/** PublicTournamentPageDto */
export type PublicTournament = {
  id: string
  name: string
  slug: string
  status: TournamentStatus
  format: TournamentFormat | null
  startAt: string
  endAt: string
  description: string | null
  /** showInDirectory: turniej na liście publicznej. Wyłączony = portal tylko z linku, bez Google. */
  settings: PublicVisibility & { showInDirectory?: boolean }
  categories: { id: string; name: string; order: number }[]
  /** stage 2: grupa drugiej fazy (format „Dwie fazy grupowe”), brak = faza 1. */
  groups: { id: string; categoryId: string; name: string; order: number; stage?: 1 | 2 }[]
  pitches: { id: string; name: string }[]
}

/** PublicTeamDto */
export type PublicTeam = {
  tournamentTeamId: string
  teamId: string
  name: string
  shortName: string | null
  categoryId: string
  groupId: string | null
  logoUrl: string | null
}

/** StandingDto */
export type Standing = {
  id: string
  tournamentId: string
  categoryId: string
  groupId: string
  tournamentTeamId: string
  matchesPlayed: number
  wins: number
  draws: number
  losses: number
  goalsFor: number
  goalsAgainst: number
  goalDifference: number
  points: number
  position: number
}

export type BracketSourceType = "Fixed" | "GroupPosition" | "MatchWinner" | "MatchLoser" | "Bye"

/** BracketDto */
export type BracketNode = {
  id: string
  bracketRoundId: string
  matchId: string | null
  homeSourceType: BracketSourceType
  homeTournamentTeamId: string | null
  awaySourceType: BracketSourceType
  awayTournamentTeamId: string | null
  winnerGoesToNodeId: string | null
  loserGoesToNodeId: string | null
  /** Slot w meczu, do którego przechodzi zwycięzca — do stabilnego ułożenia par. */
  winnerGoesToSlot: "Home" | "Away" | null
  homeSourceGroupId: string | null
  homeSourcePosition: number | null
  awaySourceGroupId: string | null
  awaySourcePosition: number | null
  /** Termin zaplanowany dla meczu, który jeszcze nie powstał (czeka na drużyny z poprzedniej rundy). */
  plannedPitchId: string | null
  plannedAt: string | null
  /** Sędzia wskazany z góry. Mecz dostanie go, gdy powstanie. */
  plannedRefereeId?: string | null
}

export type BracketRound = {
  id: string
  roundNumber: number
  roundType: "Standard" | "ThirdPlace"
  name: string
  nodes: BracketNode[]
}

export type Bracket = { id: string; tournamentId: string; rounds: BracketRound[] }

/** Jeden z dwóch slotów pary w drabince. */
export type BracketSlotRef = { nodeId: string; slot: "Home" | "Away" }

/** PublicSquadPlayerDto — skład na stronie drużyny (GET /public/tournaments/{slug}/teams/{id}/players). */
export type PublicSquadPlayer = {
  tournamentPlayerId: string
  /** Łączy zawodnika z rankingami (RankingEntry.playerId). */
  playerId: string
  firstName: string
  /** Skrócone do inicjału, gdy portal skraca nazwiska. */
  lastName: string
  shirtNumber: number | null
  position: string | null
}

/** PlayerRankingEntryDto */
export type RankingEntry = {
  playerId: string
  firstName: string
  lastName: string
  count: number
  tournamentTeamId: string | null
}

export type PublicMatch = Match

/** PublicTournamentListItemDto — karta na liście turniejów publicznych (GET /public/tournaments). */
export type PublicTournamentListItem = {
  name: string
  slug: string
  status: TournamentStatus
  startAt: string
  endAt: string
  organizer: { id: string; name: string }
  categories: string[]
  teamCount: number
  /** Mecze na żywo albo w przerwie. */
  liveMatchCount: number
}
