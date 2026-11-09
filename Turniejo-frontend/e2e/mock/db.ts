// Stan mocka backendu (w pamięci). Reset przed każdym testem: POST /__control/reset.
import { randomUUID } from "node:crypto"

export type Role = { role: string; scopeType: string; scopeId: string | null }
export type Organization = { id: string; name: string; slug: string; ownerUserId: string }
export type User = {
  id: string
  email: string
  password: string
  firstName: string
  lastName: string
  phoneNumber?: string | null
  roles: Role[]
}
export type Team = { id: string; name: string; shortName: string | null }
/** Zawodnik w kadrze drużyny (TeamRosterPlayerDto bez wyliczanych składów). */
export type RosterEntry = {
  playerId: string
  firstName: string
  lastName: string
  birthDate: string
  defaultPosition: string
  shirtNumber: number | null
  status: "Active" | "Left"
  leftAt: string | null
  /** W składzie seedowanego turnieju (trwa), więc usuwający dostaje pytanie o skład. */
  inCurrentSquad: boolean
  pastTournamentsCount: number
}
export type Tournament = {
  id: string
  organizationId: string
  name: string
  slug: string
  status: string
  format: string | null
  startAt: string
  endAt: string
  teamLimit: number
  description: string | null
  isPublic: boolean
}
export type MatchEvent = {
  id: string
  matchId: string
  type: string
  tournamentTeamId: string | null
  playerId: string | null
  relatedPlayerId: string | null
  minute: number
  second: number | null
  createdByUserId: string
  occurredAt: string
}
export type Match = {
  id: string
  tournamentId: string
  categoryId: string
  groupId: string
  pitchId: string
  home: string // tournamentTeamId
  away: string
  refereeId: string | null
  scheduledAt: string
  startedAt: string | null
  status: string
  homeScore: number
  awayScore: number
  events: MatchEvent[]
  pausedAt?: string | null
  totalPausedSeconds?: number
  resultType?: string
  winnerTournamentTeamId?: string | null
}

// Stałe id — testy odwołują się do nich w URL-ach.
export const IDS = {
  organization: "0f000000-0000-4000-8000-000000000001",
  tournament: "0f000000-0000-4000-8000-000000000010",
  category: "0f000000-0000-4000-8000-000000000020",
  group: "0f000000-0000-4000-8000-000000000030",
  pitch: "0f000000-0000-4000-8000-000000000040",
  homeTeam: "0f000000-0000-4000-8000-000000000050",
  awayTeam: "0f000000-0000-4000-8000-000000000051",
  match: "0f000000-0000-4000-8000-000000000060",
  organizer: "0f000000-0000-4000-8000-0000000000a1",
  referee: "0f000000-0000-4000-8000-0000000000a2",
  expiring: "0f000000-0000-4000-8000-0000000000a3",
  expiringNow: "0f000000-0000-4000-8000-0000000000a4",
  coach: "0f000000-0000-4000-8000-0000000000a5",
  parent: "0f000000-0000-4000-8000-0000000000a6",
  /** Adam Bramka (Orły) - dziecko rodzica z seeda. */
  child: "0f000000-0000-4000-8000-0000000000b1",
}
export const PUBLIC_SLUG = "puchar-e2e"
// Kody dostępu seedowanego turnieju (6 znaków hex, jak w Tournament.Create).
export const CODES = { referee: "AA1111", coach: "CC2222", viewer: "EE3333" }
export const PASSWORD = "Haslo1234!"

export const TEAM_NAMES: Record<string, Team> = {
  [IDS.homeTeam]: { id: "team-orly", name: "Orły E2E", shortName: "ORŁ" },
  [IDS.awayTeam]: { id: "team-lwy", name: "Lwy E2E", shortName: "LWY" },
}

function seed() {
  const now = Date.now()
  const users: User[] = [
    {
      id: IDS.organizer,
      email: "organizator@e2e.pl",
      password: PASSWORD,
      firstName: "Olga",
      lastName: "Organizator",
      roles: [
        { role: "Organizer", scopeType: "Organization", scopeId: IDS.organization },
        { role: "Organizer", scopeType: "Tournament", scopeId: IDS.tournament },
      ],
    },
    {
      // Sesja, która szybko wygasa i nie da się jej odświeżyć (jak po restarcie Keycloaka).
      id: IDS.expiring,
      email: "wygasa@e2e.pl",
      password: PASSWORD,
      firstName: "Wiktor",
      lastName: "Wygasły",
      roles: [{ role: "Viewer", scopeType: "Tournament", scopeId: IDS.tournament }],
    },
    {
      id: IDS.expiringNow,
      email: "wygasa-od-razu@e2e.pl",
      password: PASSWORD,
      firstName: "Wanda",
      lastName: "Wygasła",
      roles: [{ role: "Viewer", scopeType: "Tournament", scopeId: IDS.tournament }],
    },
    {
      id: IDS.referee,
      email: "sedzia@e2e.pl",
      password: PASSWORD,
      firstName: "Sebastian",
      lastName: "Sędzia",
      roles: [
        { role: "Referee", scopeType: "Tournament", scopeId: IDS.tournament },
        { role: "Referee", scopeType: "Match", scopeId: IDS.match },
      ],
    },
    {
      id: IDS.coach,
      email: "trener@e2e.pl",
      password: PASSWORD,
      firstName: "Tomasz",
      lastName: "Trener",
      roles: [
        { role: "Coach", scopeType: "Tournament", scopeId: IDS.tournament },
        { role: "Coach", scopeType: "TournamentTeam", scopeId: IDS.homeTeam },
      ],
    },
    {
      id: IDS.parent,
      email: "rodzic@e2e.pl",
      password: PASSWORD,
      firstName: "Renata",
      lastName: "Rodzic",
      roles: [{ role: "Parent", scopeType: "Player", scopeId: IDS.child }],
    },
  ]
  const tournaments: Tournament[] = [
    {
      id: IDS.tournament,
      organizationId: IDS.organization,
      name: "Puchar E2E",
      slug: PUBLIC_SLUG,
      status: "InProgress",
      format: "Group",
      startAt: new Date(now - 3_600_000).toISOString(),
      endAt: new Date(now + 6 * 3_600_000).toISOString(),
      teamLimit: 8,
      description: "Turniej testowy E2E",
      isPublic: true,
    },
  ]
  const matches: Match[] = [
    {
      id: IDS.match,
      tournamentId: IDS.tournament,
      categoryId: IDS.category,
      groupId: IDS.group,
      pitchId: IDS.pitch,
      home: IDS.homeTeam,
      away: IDS.awayTeam,
      refereeId: IDS.referee,
      scheduledAt: new Date(now - 600_000).toISOString(),
      startedAt: new Date(now - 540_000).toISOString(),
      status: "Live",
      homeScore: 0,
      awayScore: 0,
      events: [],
    },
  ]
  const organizations: Organization[] = [
    { id: IDS.organization, name: "Klub E2E", slug: "klub-e2e", ownerUserId: IDS.organizer },
  ]
  const rosters: Record<string, RosterEntry[]> = {
    "team-orly": [
      {
        playerId: "0f000000-0000-4000-8000-0000000000b1",
        firstName: "Adam",
        lastName: "Bramka",
        birthDate: "2014-03-01T00:00:00Z",
        defaultPosition: "Goalkeeper",
        shirtNumber: 1,
        status: "Active",
        leftAt: null,
        inCurrentSquad: true,
        pastTournamentsCount: 1,
      },
      {
        playerId: "0f000000-0000-4000-8000-0000000000b2",
        firstName: "Bartek",
        lastName: "Lewy",
        birthDate: "2014-07-12T00:00:00Z",
        defaultPosition: "Defender",
        shirtNumber: 4,
        status: "Active",
        leftAt: null,
        inCurrentSquad: false,
        pastTournamentsCount: 0,
      },
      {
        playerId: "0f000000-0000-4000-8000-0000000000b3",
        firstName: "Cezary",
        lastName: "Dawny",
        birthDate: "2013-11-20T00:00:00Z",
        defaultPosition: "Forward",
        shirtNumber: 9,
        status: "Left",
        leftAt: new Date(now - 30 * 86_400_000).toISOString(),
        inCurrentSquad: false,
        pastTournamentsCount: 2,
      },
    ],
    "team-lwy": [],
  }
  const guardians = [{ playerId: IDS.child, userId: IDS.parent, canEdit: true }]
  /** Drużyny dodane w teście (seedowane Orły i Lwy są w TEAM_NAMES). */
  const orgTeams: (Team & { logoUrl: string | null })[] = []
  return { users, tournaments, matches, organizations, rosters, guardians, orgTeams, usedResetTokens: [] as string[] }
}

export const db = seed()

export function reset() {
  Object.assign(db, seed())
}

export const newId = () => randomUUID()

export const slugify = (name: string) =>
  `${name
    .toLowerCase()
    .normalize("NFD")
    .replace(/[̀-ͯ]/g, "")
    .replace(/ł/g, "l")
    .replace(/[^a-z0-9]+/g, "-")
    .replace(/^-|-$/g, "")}-${Math.random().toString(16).slice(2, 8)}`

// --- JWT (bez podpisu — frontend tylko czyta claims) --------------------------------------------

const b64 = (value: object) => Buffer.from(JSON.stringify(value)).toString("base64url")

/** Czas życia tokenu (s) — "wygasa@e2e.pl" ma krótki token, żeby sprawdzić wygaśnięcie sesji. */
// 35 s: logowanie się udaje, ale frontend od razu próbuje odświeżyć token (margines 45 s), a serwer
// Auth.js robi to po ~5 s (margines 30 s) — odświeżenie się nie udaje, jak po restarcie Keycloaka.
// "wygasa-od-razu@e2e.pl": 1 s — sesja jest nieważna już przy pierwszym wejściu (proxy odsyła na /login
// z uszkodzoną sesją w ciasteczku; to był scenariusz pętli przeładowań).
export const tokenLifetime = (user: User) =>
  user.id === IDS.expiring ? 35 : user.id === IDS.expiringNow ? 1 : 3600

export function accessToken(user: User) {
  const payload = {
    sub: user.id,
    email: user.email,
    name: `${user.firstName} ${user.lastName}`,
    exp: Math.floor(Date.now() / 1000) + tokenLifetime(user),
  }
  return `${b64({ alg: "none", typ: "JWT" })}.${b64(payload)}.e2e`
}

export function userFromAuthHeader(header: string | null): User | undefined {
  const token = header?.replace(/^Bearer /, "")
  if (!token) return undefined
  try {
    const { sub, exp } = JSON.parse(Buffer.from(token.split(".")[1]!, "base64url").toString())
    // Jak backend: wygasły token = 401.
    if (typeof exp === "number" && exp * 1000 < Date.now()) return undefined
    return db.users.find((u) => u.id === sub)
  } catch {
    return undefined
  }
}
