// Handlery MSW udające Keycloak (token endpoint) i backend .NET — tylko endpointy, których używają
// ścieżki E2E. Kształty odpowiedzi 1:1 z DTO backendu (patrz src/types).
import { http, HttpResponse, type HttpResponseResolver } from "msw"

import {
  accessToken,
  CODES,
  db,
  IDS,
  type Match,
  newId,
  type RosterEntry,
  reset,
  slugify,
  TEAM_NAMES,
  tokenLifetime,
  type Tournament,
  type User,
  userFromAuthHeader,
} from "./db"
import { closeAll, connectionCount, negotiateResponse, publish } from "./signalr"

/**
 * Odpowiedzi ustawione przez test (POST /__control/override): test UI sam mówi, co zwraca API,
 * zamiast odtwarzać w mocku logikę backendu. Ścieżka jak w Express: /api/tournaments/:id/settings.
 * Override bez "times" działa do resetu mocka, z "times" - tyle razy.
 */
type Override = { method: string; pattern: RegExp; status: number; body: unknown; times: number | null }
let overrides: Override[] = []
/** Wywołania API od resetu ("POST /api/..."): test sprawdza np., że podwójny klik wysłał jedno żądanie. */
let requests: string[] = []

function toPattern(path: string) {
  const escaped = path.replace(/[.*+?^${}()|[\]\\]/g, "\\$&").replace(/:[A-Za-z]+/g, "[^/]+")
  return new RegExp(`${escaped}$`)
}

const overrideHandler = http.all("*", ({ request }) => {
  const url = new URL(request.url)
  if (url.pathname.startsWith("/api/")) requests.push(`${request.method} ${url.pathname}`)
  // Najnowszy override wygrywa - test może zmienić odpowiedź w trakcie.
  const hit = overrides.findLast((o) => o.method === request.method && o.pattern.test(url.pathname))
  if (!hit) return undefined
  if (hit.times !== null && --hit.times <= 0) overrides = overrides.filter((o) => o !== hit)
  return hit.body === null
    ? new HttpResponse(null, { status: hit.status })
    : HttpResponse.json(hit.body as never, { status: hit.status })
})

const STATUSES = [
  "Draft",
  "RegistrationOpen",
  "RegistrationClosed",
  "ScheduleGenerated",
  "InProgress",
  "Finished",
  "Cancelled",
]
const TRANSITIONS: Record<string, string[]> = {
  Draft: ["RegistrationOpen", "Cancelled"],
  RegistrationOpen: ["RegistrationClosed", "Cancelled"],
  RegistrationClosed: ["ScheduleGenerated", "Cancelled"],
  ScheduleGenerated: ["InProgress", "Cancelled"],
  InProgress: ["Finished", "Cancelled"],
  Finished: [],
  Cancelled: [],
}
const GOAL_TYPES: Record<number, string> = { 5: "Goal", 6: "OwnGoal", 7: "PenaltyGoal" }

// --- Pomocnicze ------------------------------------------------------------------------------

const problem = (status: number, detail: string) =>
  HttpResponse.json({ status, title: detail, detail }, { status })

/** Handler wymagający zalogowania — 401 bez poprawnego Bearer tokenu (jak backend). */
function authed<P extends Record<string, string>>(
  resolver: (args: { user: User; params: P; request: Request }) => Response | Promise<Response>,
): HttpResponseResolver<P> {
  return ({ request, params }) => {
    const user = userFromAuthHeader(request.headers.get("Authorization"))
    if (!user) return problem(401, "Unauthorized")
    return resolver({ user, params: params as P, request })
  }
}

const hasRole = (user: User, role: string, scopeType: string, scopeId: string) =>
  user.roles.some((r) => r.role === role && r.scopeType === scopeType && r.scopeId === scopeId)

// Kadra drużyny: dostęp ma organizator organizacji (w mocku wszystkie drużyny należą do jednej).
const POSITIONS: Record<number, string> = {
  1: "Goalkeeper",
  2: "Defender",
  3: "Midfielder",
  4: "Forward",
  5: "Universal",
  6: "Unknown",
}
type RosterBody = {
  firstName: string
  lastName: string
  birthDate: string
  defaultPosition: number
  shirtNumber: number | null
}

function rosterOf(user: User, teamId: string) {
  if (!hasRole(user, "Organizer", "Organization", IDS.organization)) return undefined
  return db.rosters[teamId]
}

const numberTaken = (roster: RosterEntry[], shirtNumber: number | null, exceptId: string | null) =>
  shirtNumber !== null &&
  roster.some(
    (p) => p.status === "Active" && p.playerId !== exceptId && p.shirtNumber === shirtNumber,
  )

const numberTakenProblem = () =>
  HttpResponse.json(
    {
      status: 422,
      title: "Validation",
      errors: [{ field: "ShirtNumber", message: "Shirt number is already taken in this team" }],
    },
    { status: 422 },
  )

function rosterDto({ inCurrentSquad, ...entry }: RosterEntry) {
  const tournament = db.tournaments.find((t) => t.id === IDS.tournament)!
  return {
    ...entry,
    photoUrl: null,
    currentSquads: inCurrentSquad
      ? [
          {
            tournamentTeamId: IDS.homeTeam,
            tournamentId: tournament.id,
            tournamentName: tournament.name,
            tournamentStatus: tournament.status,
            tournamentStartAt: tournament.startAt,
            canEditSquad: true,
          },
        ]
      : [],
  }
}

const tournamentDto = (t: Tournament, user?: User) => {
  const organizer = user && hasRole(user, "Organizer", "Tournament", t.id)
  return {
    id: t.id,
    name: t.name,
    slug: t.slug,
    status: t.status,
    format: t.format,
    startAt: t.startAt,
    endAt: t.endAt,
    teamLimit: t.teamLimit,
    isPaid: false,
    refereeCode: organizer ? CODES.referee : null,
    coachCode: organizer ? CODES.coach : null,
    viewerCode: organizer ? CODES.viewer : null,
    description: t.description,
    isPublic: t.isPublic,
  }
}

const findTournament = (id: string) => db.tournaments.find((t) => t.id === id)
const findMatch = (id: string) => db.matches.find((m) => m.id === id)

const matchDto = (m: Match) => ({
  id: m.id,
  tournamentId: m.tournamentId,
  categoryId: m.categoryId,
  groupId: m.groupId,
  pitchId: m.pitchId,
  homeTournamentTeamId: m.home,
  awayTournamentTeamId: m.away,
  refereeId: m.refereeId,
  scheduledAt: m.scheduledAt,
  status: m.status,
  stageType: "Group",
  homeScore: m.homeScore,
  awayScore: m.awayScore,
  resultType: m.resultType ?? (m.status === "Finished" ? "Regular" : "NotPlayed"),
  homePenaltyScore: null,
  awayPenaltyScore: null,
})

const elapsed = (m: Match) => {
  if (!m.startedAt) return 0
  const until = m.pausedAt ? new Date(m.pausedAt).getTime() : Date.now()
  const seconds = (until - new Date(m.startedAt).getTime()) / 1000 - (m.totalPausedSeconds ?? 0)
  return Math.max(0, Math.floor(seconds))
}

const matchTeam = (tournamentTeamId: string) => ({
  tournamentTeamId,
  teamId: TEAM_NAMES[tournamentTeamId]!.id,
  name: TEAM_NAMES[tournamentTeamId]!.name,
  shortName: TEAM_NAMES[tournamentTeamId]!.shortName,
  players: squadEntries(tournamentTeamId)
    .filter((p) => p.status !== "Removed")
    .map((p) => ({
      tournamentPlayerId: p.id,
      playerId: p.playerId,
      firstName: p.firstName,
      lastName: p.lastName,
      shirtNumber: p.shirtNumber,
      position: p.position,
      status: p.status,
    })),
})

const livePanel = (m: Match) => ({
  matchId: m.id,
  tournamentId: m.tournamentId,
  status: m.status,
  stageType: "Group",
  scheduledAt: m.scheduledAt,
  startedAt: m.startedAt,
  pausedAt: m.pausedAt ?? null,
  finishedAt: null,
  totalPausedSeconds: m.totalPausedSeconds ?? 0,
  homeScore: m.homeScore,
  awayScore: m.awayScore,
  resultType: m.resultType ?? "NotPlayed",
  winnerTournamentTeamId: m.winnerTournamentTeamId ?? null,
  homeTeam: matchTeam(m.home),
  awayTeam: matchTeam(m.away),
  events: m.events,
  editLockedAt: m.editLockedAt ?? null,
  elapsedSeconds: elapsed(m),
  homePenaltyScore: null,
  awayPenaltyScore: null,
})

const rosterPlayer = (playerId: string) =>
  Object.values(db.rosters)
    .flat()
    .find((p) => p.playerId === playerId)

/** Skład zgłoszenia - ten sam, który edytuje organizator (sekcja „Skład drużyny w turnieju”). */
const squadOf = (tournamentTeamId: string) => squadEntries(tournamentTeamId)

const teamMatch = (m: Match) => ({
  matchId: m.id,
  scheduledAt: m.scheduledAt,
  status: m.status,
  homeTeamName: TEAM_NAMES[m.home]!.name,
  awayTeamName: TEAM_NAMES[m.away]!.name,
  homeScore: m.homeScore,
  awayScore: m.awayScore,
})

const tournamentTeams = (tournamentId: string) =>
  tournamentId !== IDS.tournament
    ? []
    : Object.entries(TEAM_NAMES).map(([id, team]) => ({
        id,
        tournamentId,
        teamId: team.id,
        categoryId: IDS.category,
        groupId: IDS.group,
        registrationStatus: "Accepted",
        paymentStatus: "NotRequired",
        seedNumber: null,
        teamName: team.name,
        teamShortName: team.shortName,
        teamLogoUrl: null,
        ...db.teamChanges[id],
      }))

/**
 * Tabela grupy jak GroupTable w backendzie: wszystkie zaakceptowane drużyny grupy, także przed
 * pierwszym meczem (z zerami). Kolejność: punkty, różnica bramek, bramki zdobyte.
 */
function groupStandings(tournamentId: string, groupId: string) {
  const finished = db.matches.filter((m) => m.groupId === groupId && m.status === "Finished")
  const rows = tournamentTeams(tournamentId)
    .filter((t) => t.groupId === groupId && t.registrationStatus === "Accepted")
    .map((t) => {
      const row = { matchesPlayed: 0, wins: 0, draws: 0, losses: 0, goalsFor: 0, goalsAgainst: 0 }
      for (const m of finished.filter((x) => x.home === t.id || x.away === t.id)) {
        const [own, other] = m.home === t.id ? [m.homeScore, m.awayScore] : [m.awayScore, m.homeScore]
        row.matchesPlayed++
        row.goalsFor += own
        row.goalsAgainst += other
        if (own > other) row.wins++
        else if (own === other) row.draws++
        else row.losses++
      }
      return {
        id: `${groupId}-${t.id}`,
        tournamentId,
        categoryId: t.categoryId,
        groupId,
        tournamentTeamId: t.id,
        ...row,
        goalDifference: row.goalsFor - row.goalsAgainst,
        points: row.wins * 3 + row.draws,
      }
    })
    .sort((a, b) => b.points - a.points || b.goalDifference - a.goalDifference || b.goalsFor - a.goalsFor)
  return rows.map((r, i) => ({ ...r, position: i + 1 }))
}

/** Po zakończonym meczu backend przelicza tabelę i wysyła StandingUpdated do grupy turnieju. */
function standingChanged(m: Match) {
  publish("tournaments", m.tournamentId, "StandingUpdated", { tournamentId: m.tournamentId, groupId: m.groupId })
}

function createOrganization(user: User, name: string) {
  const organization = { id: newId(), name, slug: slugify(name), ownerUserId: user.id }
  db.organizations.push(organization)
  user.roles.push({ role: "Organizer", scopeType: "Organization", scopeId: organization.id })
  return organization
}

/** Gol: wynik + zdarzenie + te same powiadomienia SignalR, które wysyła backend. */
// Wzięcie, oddanie i przydzielenie sędziego idą do grupy turnieju (MatchChanged), jak w backendzie.
function refereeChanged(m: (typeof db.matches)[number]) {
  publish("tournaments", m.tournamentId, "MatchChanged", {
    tournamentId: m.tournamentId,
    matchId: m.id,
  })
}

function releaseReferee(m: (typeof db.matches)[number]) {
  const referee = db.users.find((u) => u.id === m.refereeId)
  if (referee)
    referee.roles = referee.roles.filter(
      (r) => !(r.role === "Referee" && r.scopeType === "Match" && r.scopeId === m.id),
    )
  m.refereeId = null
  refereeChanged(m)
}

export function scoreGoal(
  m: Match,
  tournamentTeamId: string,
  userId: string,
  minute: number,
  type = "Goal",
) {
  const scorerIsHome =
    type === "OwnGoal" ? tournamentTeamId !== m.home : tournamentTeamId === m.home
  if (scorerIsHome) m.homeScore++
  else m.awayScore++
  const event = {
    id: newId(),
    matchId: m.id,
    type,
    tournamentTeamId,
    playerId: null,
    relatedPlayerId: null,
    minute,
    second: null,
    createdByUserId: userId,
    occurredAt: new Date().toISOString(),
  }
  m.events.push(event)
  const score = { matchId: m.id, homeScore: m.homeScore, awayScore: m.awayScore }
  publish("matches", m.id, "MatchScoreChanged", score)
  publish("matches", m.id, "MatchEventAdded", { matchId: m.id, matchEventId: event.id })
  publish("tournaments", m.tournamentId, "MatchChanged", { ...score, tournamentId: m.tournamentId })
  return event
}

// --- Keycloak --------------------------------------------------------------------------------

const keycloak = [
  http.post("*/realms/:realm/protocol/openid-connect/token", async ({ request }) => {
    const form = new URLSearchParams(await request.text())
    let user: User | undefined
    if (form.get("grant_type") === "password") {
      user = db.users.find(
        (u) =>
          u.email === form.get("username")?.toLowerCase() && u.password === form.get("password"),
      )
    } else if (form.get("grant_type") === "refresh_token") {
      user = db.users.find((u) => `refresh.${u.id}` === form.get("refresh_token"))
      // Sesja w Keycloaku już nie istnieje — odświeżenie się nie udaje.
      if (user?.id === IDS.expiring || user?.id === IDS.expiringNow) user = undefined
    }
    if (!user) {
      return HttpResponse.json(
        { error: "invalid_grant", error_description: "Invalid user credentials" },
        { status: 401 },
      )
    }
    return HttpResponse.json({
      access_token: accessToken(user),
      refresh_token: `refresh.${user.id}`,
      expires_in: tokenLifetime(user),
      token_type: "Bearer",
    })
  }),
  http.post(
    "*/realms/:realm/protocol/openid-connect/logout",
    () => new HttpResponse(null, { status: 204 }),
  ),
]

// --- Backend ---------------------------------------------------------------------------------

const api = [
  // Jak ConfirmPasswordResetCommandHandler; rolę maila z linkiem gra stały token "token-<email>".
  // Jak backend: ta sama odpowiedź dla istniejącego i nieznanego e-maila (nie zdradza, czy konto jest).
  http.post("*/api/auth/password-reset/request", () => new HttpResponse(null, { status: 204 })),
  http.post("*/api/auth/password-reset/confirm", async ({ request }) => {
    const body = (await request.json()) as { token: string; newPassword: string }
    const user = db.users.find((u) => `token-${u.email}` === body.token)
    if (!user || db.usedResetTokens.includes(body.token)) {
      return HttpResponse.json(
        { code: "Validation.Token", message: "Reset link is invalid or has expired.", type: 2 },
        { status: 400 },
      )
    }
    db.usedResetTokens.push(body.token)
    user.password = body.newPassword
    return new HttpResponse(null, { status: 204 })
  }),
  http.post("*/api/auth/register", async ({ request }) => {
    const body = (await request.json()) as Record<string, string | boolean | null>
    const email = String(body.email).toLowerCase()
    // Jak RegisterUserCommandValidator: bez akceptacji Regulaminu konto nie powstaje.
    if (body.acceptTerms !== true) {
      return HttpResponse.json(
        { code: "Validation.AcceptTerms", message: "You must accept the terms.", type: 2 },
        { status: 400 },
      )
    }
    if (db.users.some((u) => u.email === email)) {
      return HttpResponse.json(
        { code: "Validation.Email", message: "User with this email already exists.", type: 2 },
        { status: 400 },
      )
    }
    const user: User = {
      id: newId(),
      email,
      password: String(body.password),
      firstName: String(body.firstName),
      lastName: String(body.lastName),
      roles: [],
    }
    db.users.push(user)
    // Jak UserRegisteredHandler: organizacja + rola tylko dla organizatora (domyślnie true).
    if (body.createOrganization !== false) {
      createOrganization(
        user,
        String(body.organizationName ?? "").trim() || `${user.firstName} ${user.lastName}`,
      )
    }
    return HttpResponse.json({ userId: user.id }, { status: 201 })
  }),

  http.get(
    "*/api/me",
    authed(({ user }) =>
      HttpResponse.json({ userId: user.id, email: user.email, isAuthenticated: true }),
    ),
  ),
  http.get(
    "*/api/me/roles",
    authed(({ user }) => HttpResponse.json(user.roles)),
  ),
  // Jak GetMyProfileQuery / UpdateMyProfileCommand / ChangeMyPasswordCommand.
  http.get(
    "*/api/me/profile",
    authed(({ user }) =>
      HttpResponse.json({
        email: user.email,
        firstName: user.firstName,
        lastName: user.lastName,
        phoneNumber: user.phoneNumber ?? null,
        seenReleaseVersion: user.seenReleaseVersion ?? null,
        theme: user.theme ?? "system",
      }),
    ),
  ),
  http.put(
    "*/api/me/profile",
    authed(async ({ user, request }) => {
      const body = (await request.json()) as {
        firstName: string
        lastName: string
        phoneNumber: string | null
      }
      user.firstName = body.firstName.trim()
      user.lastName = body.lastName.trim()
      user.phoneNumber = body.phoneNumber?.trim() || null
      return new HttpResponse(null, { status: 204 })
    }),
  ),
  // Jak MarkReleaseSeenCommand: tylko X.Y.Z i tylko w przód.
  http.put(
    "*/api/me/seen-release",
    authed(async ({ user, request }) => {
      const { version } = (await request.json()) as { version: string }
      if (!/^\d+\.\d+\.\d+$/.test(version)) return problem(422, "Release version must look like 1.2.0.")
      const newer = (a: string, b: string) => {
        const [x, y] = [a, b].map((v) => v.split(".").map(Number))
        const i = x!.findIndex((n, k) => n !== y![k])
        return i >= 0 && x![i]! > y![i]!
      }
      if (!user.seenReleaseVersion || newer(version, user.seenReleaseVersion)) user.seenReleaseVersion = version
      return new HttpResponse(null, { status: 204 })
    }),
  ),
  // Jak SetMyThemeCommand: tylko system, light albo dark.
  http.put(
    "*/api/me/theme",
    authed(async ({ user, request }) => {
      const { theme } = (await request.json()) as { theme: string }
      if (theme !== "system" && theme !== "light" && theme !== "dark")
        return problem(422, "Theme must be system, light or dark.")
      user.theme = theme
      return new HttpResponse(null, { status: 204 })
    }),
  ),
  http.post(
    "*/api/me/password",
    authed(async ({ user, request }) => {
      const body = (await request.json()) as { currentPassword: string; newPassword: string }
      if (body.currentPassword !== user.password) {
        return HttpResponse.json(
          {
            code: "Validation.CurrentPassword",
            message: "Current password is incorrect.",
            type: 2,
          },
          { status: 400 },
        )
      }
      user.password = body.newPassword
      return new HttpResponse(null, { status: 204 })
    }),
  ),
  http.get(
    "*/api/me/export",
    authed(({ user }) =>
      HttpResponse.json({
        exportedAt: new Date().toISOString(),
        profile: {
          id: user.id,
          email: user.email,
          firstName: user.firstName,
          lastName: user.lastName,
        },
        roles: user.roles,
        children: [],
      }),
    ),
  ),
  // Jak DeleteMyAccountCommandHandler: jedyny organizator organizacji → 409, dopóki nie potwierdzi
  // usunięcia organizacji (?deleteOrganization=true); inaczej konto znika.
  http.delete(
    "*/api/me",
    authed(({ user, request }) => {
      const organization = db.organizations.find((o) => o.ownerUserId === user.id)
      if (organization) {
        if (new URL(request.url).searchParams.get("deleteOrganization") !== "true") {
          return HttpResponse.json(
            { code: "Conflict.Account", message: "You are the only organizer.", type: 3 },
            { status: 409 },
          )
        }
        db.organizations.splice(db.organizations.indexOf(organization), 1)
      }
      db.users.splice(db.users.indexOf(user), 1)
      return new HttpResponse(null, { status: 204 })
    }),
  ),
  http.get(
    "*/api/notifications",
    authed(() => HttpResponse.json([])),
  ),

  http.get(
    "*/api/organizations/mine",
    authed(({ user }) => {
      const organization = db.organizations.find((o) => o.ownerUserId === user.id)
      return organization
        ? HttpResponse.json({
            id: organization.id,
            name: organization.name,
            slug: organization.slug,
          })
        : new HttpResponse(null, { status: 204 })
    }),
  ),
  http.put(
    "*/api/organizations/:id",
    authed<{ id: string }>(async ({ request, user, params }) => {
      const organization = db.organizations.find((o) => o.id === params.id)
      if (!organization || !hasRole(user, "Organizer", "Organization", organization.id)) {
        return problem(403, "Forbidden")
      }
      organization.name = ((await request.json()) as { name: string }).name.trim()
      return new HttpResponse(null, { status: 204 })
    }),
  ),
  http.post(
    "*/api/organizations",
    authed(async ({ request, user }) => {
      const { name } = (await request.json()) as { name: string }
      if (db.organizations.some((o) => o.ownerUserId === user.id)) {
        return HttpResponse.json(
          {
            code: "Organization.AlreadyExists",
            message: "User already owns an organization.",
            type: 3,
          },
          { status: 409 },
        )
      }
      const organization = createOrganization(user, name)
      return HttpResponse.json(
        { organizationId: organization.id, slug: organization.slug },
        { status: 201 },
      )
    }),
  ),
  http.post(
    "*/api/tournaments/join",
    authed(async ({ request, user }) => {
      const { code } = (await request.json()) as { code: string }
      const role =
        code === CODES.referee
          ? "Referee"
          : code === CODES.coach
            ? "Coach"
            : code === CODES.viewer
              ? "Viewer"
              : null
      const tournament = findTournament(IDS.tournament)!
      if (!role) {
        return HttpResponse.json(
          { code: "Validation.Code", message: "Invalid tournament code.", type: 2 },
          { status: 400 },
        )
      }
      if (!hasRole(user, role, "Tournament", tournament.id)) {
        user.roles.push({ role, scopeType: "Tournament", scopeId: tournament.id })
      }
      return HttpResponse.json({
        tournamentId: tournament.id,
        tournamentName: tournament.name,
        slug: tournament.slug,
        role,
        isPublic: tournament.isPublic,
      })
    }),
  ),

  http.get(
    "*/api/tournaments",
    authed(({ request, user }) => {
      const organizationId = new URL(request.url).searchParams.get("organizationId")
      return HttpResponse.json(
        db.tournaments
          .filter((t) => t.organizationId === organizationId)
          .map((t) => tournamentDto(t, user)),
      )
    }),
  ),

  http.post(
    "*/api/tournaments",
    authed(async ({ request, user }) => {
      const body = (await request.json()) as Record<string, string | number | null>
      if (!body.name) {
        return HttpResponse.json(
          {
            status: 422,
            title: "Validation",
            errors: [{ field: "Name", message: "Name is required." }],
          },
          { status: 422 },
        )
      }
      const tournament: Tournament = {
        id: newId(),
        organizationId: String(body.organizationId),
        name: String(body.name),
        slug: slugify(String(body.name)),
        status: "Draft",
        format: null,
        startAt: String(body.startAt),
        endAt: String(body.endAt),
        teamLimit: Number(body.teamLimit ?? 16),
        description: (body.description as string | null) ?? null,
        isPublic: false,
      }
      db.tournaments.push(tournament)
      user.roles.push({ role: "Organizer", scopeType: "Tournament", scopeId: tournament.id })
      return HttpResponse.json(
        { tournamentId: tournament.id, slug: tournament.slug },
        { status: 201 },
      )
    }),
  ),

  http.get(
    "*/api/tournaments/:id",
    authed<{ id: string }>(({ params, user }) => {
      const t = findTournament(params.id)
      return t ? HttpResponse.json(tournamentDto(t, user)) : problem(404, "Tournament not found")
    }),
  ),

  http.get(
    "*/api/tournaments/:id/dashboard",
    authed<{ id: string }>(({ params }) => {
      const t = findTournament(params.id)
      if (!t) return problem(404, "Tournament not found")
      const matches = db.matches.filter((m) => m.tournamentId === t.id)
      const teams = tournamentTeams(t.id).length
      return HttpResponse.json({
        tournamentId: t.id,
        tournamentName: t.name,
        tournamentStatus: t.status,
        teams: { pending: 0, accepted: teams, rejected: 0, withdrawn: 0, total: teams },
        matches: {
          finished: matches.filter((m) => m.status === "Finished").length,
          scheduled: matches.filter((m) => m.status !== "Finished").length,
          total: matches.length,
        },
        scheduleStatus: matches.length ? "InProgress" : "NotGenerated",
        upcomingMatches: [],
      })
    }),
  ),

  http.patch(
    "*/api/tournaments/:id/status",
    authed<{ id: string }>(async ({ params, request, user }) => {
      const t = findTournament(params.id)
      if (!t) return problem(404, "Tournament not found")
      if (!hasRole(user, "Organizer", "Tournament", t.id)) return problem(403, "Forbidden")
      const { tournamentStatus } = (await request.json()) as { tournamentStatus: number }
      const next = STATUSES[tournamentStatus - 1]
      if (!next || !TRANSITIONS[t.status]!.includes(next)) {
        return HttpResponse.json(
          {
            code: "Tournament.InvalidStatusTransition",
            message: `Cannot change status from ${t.status} to ${next}.`,
            type: 2,
          },
          { status: 400 },
        )
      }
      t.status = next
      return new HttpResponse(null, { status: 204 })
    }),
  ),

  http.get(
    "*/api/tournaments/:id/settings",
    authed(() =>
      HttpResponse.json({
        pointsForWin: 3,
        pointsForDraw: 1,
        pointsForLoss: 0,
        matchDurationMinutes: 20,
        breakDurationMinutes: 5,
        isThirdPlaceMatchEnabled: false,
        refereeEditWindowMinutes: 15,
        walkoverHomeScore: 3,
        walkoverAwayScore: 0,
        canCoachEditSquadAfterStart: false,
      }),
    ),
  ),
  http.get(
    "*/api/tournaments/:id/teams",
    authed<{ id: string }>(({ params }) => HttpResponse.json(tournamentTeams(params.id))),
  ),
  http.get(
    "*/api/tournaments/:id/pitches",
    authed<{ id: string }>(({ params }) =>
      HttpResponse.json(
        params.id === IDS.tournament
          ? [
              {
                id: IDS.pitch,
                tournamentId: params.id,
                name: "Boisko Główne",
                description: null,
                isActive: true,
              },
            ]
          : [],
      ),
    ),
  ),

  // --- Sędzia / panel live ---
  http.get(
    "*/api/referee/matches",
    authed(({ user }) =>
      HttpResponse.json(db.matches.filter((m) => m.refereeId === user.id).map(matchDto)),
    ),
  ),
  http.get(
    "*/api/referee/matches/open",
    authed(({ user }) =>
      HttpResponse.json(
        db.matches
          .filter(
            (m) =>
              !m.refereeId &&
              m.status !== "Finished" &&
              m.status !== "Cancelled" &&
              hasRole(user, "Referee", "Tournament", m.tournamentId),
          )
          .map(matchDto),
      ),
    ),
  ),
  http.post(
    "*/api/tournaments/:tid/matches/:mid/referee/claim",
    authed<{ tid: string; mid: string }>(({ params, user }) => {
      const m = findMatch(params.mid)
      if (!m) return problem(404, "Match not found")
      if (!hasRole(user, "Referee", "Tournament", m.tournamentId)) return problem(403, "Forbidden")
      if (m.refereeId) return problem(409, "Match already has a referee")
      m.refereeId = user.id
      user.roles.push({ role: "Referee", scopeType: "Match", scopeId: m.id })
      refereeChanged(m)
      return HttpResponse.json({ matchId: m.id })
    }),
  ),
  http.post(
    "*/api/tournaments/:tid/matches/:mid/referee/release",
    authed<{ tid: string; mid: string }>(({ params, user }) => {
      const m = findMatch(params.mid)
      if (!m) return problem(404, "Match not found")
      if (m.refereeId !== user.id) return problem(403, "Forbidden")
      releaseReferee(m)
      return HttpResponse.json({ matchId: m.id })
    }),
  ),
  http.get(
    "*/api/matches/:id/location",
    authed<{ id: string }>(({ params }) => {
      const m = findMatch(params.id)
      if (!m) return problem(404, "Match not found")
      return HttpResponse.json({
        matchId: m.id,
        tournamentId: m.tournamentId,
        scheduledAt: m.scheduledAt,
        pitchId: m.pitchId,
        pitchName: "Boisko Główne",
        pitchDescription: null,
      })
    }),
  ),
  http.get(
    "*/api/tournaments/:tid/matches/:mid/live-panel",
    authed<{ tid: string; mid: string }>(({ params }) => {
      const m = findMatch(params.mid)
      return m ? HttpResponse.json(livePanel(m)) : problem(404, "Match not found")
    }),
  ),
  http.get(
    "*/api/tournaments/:tid/matches/:mid/clock",
    authed<{ tid: string; mid: string }>(({ params }) => {
      const m = findMatch(params.mid)
      return m
        ? HttpResponse.json({ matchId: m.id, status: m.status, elapsedSeconds: elapsed(m) })
        : problem(404, "Match not found")
    }),
  ),
  // Start, pauza, wznowienie i koniec: tylko sędzia meczu albo organizator, w dozwolonym stanie (inaczej 409).
  ...(
    [
      ["start", ["Scheduled"], "Live"],
      ["pause", ["Live"], "Paused"],
      ["resume", ["Paused"], "Live"],
      ["finish", ["Live", "Paused"], "Finished"],
    ] as const
  ).map(([action, from, to]) =>
    http.post(
      `*/api/tournaments/:tid/matches/:mid/${action}`,
      authed<{ tid: string; mid: string }>(async ({ params, request, user }) => {
        const m = findMatch(params.mid)
        if (!m) return problem(404, "Match not found")
        if (m.refereeId !== user.id && !hasRole(user, "Organizer", "Tournament", m.tournamentId)) {
          return problem(403, "Forbidden")
        }
        if (!(from as readonly string[]).includes(m.status)) {
          return problem(409, `Cannot ${action} a match that is ${m.status}.`)
        }
        const now = new Date().toISOString()
        if (action === "start") m.startedAt = now
        if (action === "pause") m.pausedAt = now
        if (action === "resume" && m.pausedAt) {
          m.totalPausedSeconds =
            (m.totalPausedSeconds ?? 0) + (Date.now() - new Date(m.pausedAt).getTime()) / 1000
          m.pausedAt = null
        }
        if (action === "finish") {
          const body = (await request.json().catch(() => ({}))) as {
            resultType?: string | number
            winnerTournamentTeamId?: string | null
          }
          const RESULT_TYPES = ["NotPlayed", "Regular", "ExtraTime", "Penalties", "Walkover", "ManualDecision"]
          m.resultType =
            typeof body.resultType === "string"
              ? body.resultType
              : (RESULT_TYPES[(body.resultType ?? 2) - 1] ?? "Regular")
          m.winnerTournamentTeamId = body.winnerTournamentTeamId ?? null
          m.pausedAt = null
        }
        m.status = to
        publish("matches", m.id, "MatchStatusChanged", { matchId: m.id, status: to })
        publish("tournaments", m.tournamentId, "MatchChanged", { tournamentId: m.tournamentId, matchId: m.id })
        if (to === "Finished") standingChanged(m)
        return HttpResponse.json({ matchId: m.id, status: to })
      }),
    ),
  ),
  // Kartka: druga żółta tego samego zawodnika dokłada czerwoną (jak backend, IsSecondYellow).
  http.post(
    "*/api/tournaments/:tid/matches/:mid/cards",
    authed<{ tid: string; mid: string }>(async ({ params, request, user }) => {
      const m = findMatch(params.mid)
      if (!m) return problem(404, "Match not found")
      if (m.status !== "Live" && m.status !== "Paused") return problem(400, "Match is not in progress.")
      const body = (await request.json()) as { tournamentTeamId: string; playerId: string; cardType: number | string; minute: number }
      const type = typeof body.cardType === "number" ? (body.cardType === 9 ? "RedCard" : "YellowCard") : body.cardType
      const sentOff = m.events.some((e) => e.playerId === body.playerId && e.type === "RedCard")
      if (sentOff) return validation("PlayerId", "Player has been sent off")
      const event = (eventType: string) => {
        const e = {
          id: newId(),
          matchId: m.id,
          type: eventType,
          tournamentTeamId: body.tournamentTeamId,
          playerId: body.playerId,
          relatedPlayerId: null,
          minute: body.minute,
          second: null,
          createdByUserId: user.id,
          occurredAt: new Date().toISOString(),
        }
        m.events.push(e)
        return e
      }
      const card = event(type)
      if (type === "YellowCard" && m.events.filter((e) => e.playerId === body.playerId && e.type === "YellowCard").length === 2)
        event("RedCard")
      publish("matches", m.id, "MatchEventAdded", { matchId: m.id, matchEventId: card.id })
      return HttpResponse.json({ matchEventId: card.id })
    }),
  ),
  http.put(
    "*/api/tournaments/:tid/matches/:mid/events/:eid",
    authed<{ tid: string; mid: string; eid: string }>(async ({ params, request }) => {
      const m = findMatch(params.mid)
      const e = m?.events.find((x) => x.id === params.eid)
      if (!m || !e) return problem(404, "Event not found")
      const body = (await request.json()) as { minute: number; second: number | null; playerId: string | null }
      if (body.minute < 0 || body.minute > 300) return validation("Minute", "Minute must be between 0 and 300.")
      Object.assign(e, { minute: body.minute, second: body.second ?? null, playerId: body.playerId ?? e.playerId })
      publish("matches", m.id, "MatchEventAdded", { matchId: m.id, matchEventId: e.id })
      return HttpResponse.json({ matchEventId: e.id })
    }),
  ),
  http.post(
    "*/api/tournaments/:tid/matches/:mid/mvp",
    authed<{ tid: string; mid: string }>(async ({ params, request, user }) => {
      const m = findMatch(params.mid)
      if (!m) return problem(404, "Match not found")
      const body = (await request.json()) as { tournamentTeamId: string; playerId: string }
      m.events = m.events.filter((e) => e.type !== "MvpSelected")
      const e = {
        id: newId(),
        matchId: m.id,
        type: "MvpSelected",
        tournamentTeamId: body.tournamentTeamId,
        playerId: body.playerId,
        relatedPlayerId: null,
        minute: 0,
        second: null,
        createdByUserId: user.id,
        occurredAt: new Date().toISOString(),
      }
      m.events.push(e)
      return HttpResponse.json({ matchEventId: e.id })
    }),
  ),
  http.post(
    "*/api/tournaments/:tid/matches/:mid/goals",
    authed<{ tid: string; mid: string }>(async ({ params, request, user }) => {
      const m = findMatch(params.mid)
      if (!m) return problem(404, "Match not found")
      if (m.refereeId !== user.id && !hasRole(user, "Organizer", "Tournament", m.tournamentId)) {
        return problem(403, "Forbidden")
      }
      if (m.status !== "Live" && m.status !== "Paused")
        return problem(400, "Match is not in progress.")
      const body = (await request.json()) as {
        tournamentTeamId: string
        minute: number
        goalType: number
      }
      const event = scoreGoal(
        m,
        body.tournamentTeamId,
        user.id,
        body.minute,
        GOAL_TYPES[body.goalType] ?? "Goal",
      )
      return HttpResponse.json({
        matchEventId: event.id,
        homeScore: m.homeScore,
        awayScore: m.awayScore,
      })
    }),
  ),

  // --- Panel trenera i rodzica (z seeda: Orły, skład = zawodnicy kadry w bieżącym składzie) ---
  http.get(
    "*/api/coach/teams",
    authed(({ user }) => {
      const t = findTournament(IDS.tournament)!
      return HttpResponse.json(
        user.roles
          .filter((r) => r.scopeType === "TournamentTeam" && r.role === "Coach")
          .map((r) => ({
            tournamentTeamId: r.scopeId,
            tournamentId: t.id,
            tournamentName: t.name,
            teamId: TEAM_NAMES[r.scopeId!]!.id,
            teamName: TEAM_NAMES[r.scopeId!]!.name,
            categoryId: IDS.category,
            groupId: IDS.group,
            role: "Coach",
            tournamentStatus: t.status,
            canEditSquad: t.status !== "InProgress" && t.status !== "Finished",
          })),
      )
    }),
  ),
  http.get(
    "*/api/coach/teams/:id/:part",
    authed<{ id: string; part: string }>(({ params, user }) => {
      if (!hasRole(user, "Coach", "TournamentTeam", params.id)) return problem(403, "Forbidden")
      const squad = squadOf(params.id)
      if (params.part === "players") return HttpResponse.json(squad)
      const matches = db.matches
        .filter((m) => m.home === params.id || m.away === params.id)
        .map(teamMatch)
      if (params.part === "schedule") return HttpResponse.json(matches)
      if (params.part === "results")
        return HttpResponse.json(matches.filter((m) => m.status === "Finished"))
      return HttpResponse.json(
        squad.map((p) => ({
          tournamentPlayerId: p.id,
          playerId: p.playerId,
          firstName: p.firstName,
          lastName: p.lastName,
          goals: db.matches
            .flatMap((m) => m.events)
            .filter((e) => e.playerId === p.playerId && e.type === "Goal").length,
          assists: 0,
          yellowCards: 0,
          redCards: 0,
        })),
      )
    }),
  ),
  http.get(
    "*/api/parent/players",
    authed(({ user }) =>
      HttpResponse.json(
        db.guardians
          .filter((g) => g.userId === user.id)
          .map((g) => {
            const p = rosterPlayer(g.playerId)!
            return {
              playerId: p.playerId,
              firstName: p.firstName,
              lastName: p.lastName,
              birthDate: p.birthDate,
              defaultPosition: p.defaultPosition,
              photoUrl: null,
              relationshipType: "Parent",
              canEdit: g.canEdit,
            }
          }),
      ),
    ),
  ),
  http.get(
    "*/api/parent/players/:id/:part",
    authed<{ id: string; part: string }>(({ params, user }) => {
      if (!db.guardians.some((g) => g.userId === user.id && g.playerId === params.id))
        return problem(403, "Forbidden")
      const t = findTournament(IDS.tournament)!
      const p = rosterPlayer(params.id)!
      const team = IDS.homeTeam
      const childMatch = (m: Match) => ({
        ...teamMatch(m),
        tournamentId: t.id,
        tournamentName: t.name,
      })
      const matches = db.matches.filter((m) => m.home === team || m.away === team)
      if (params.part === "team")
        return HttpResponse.json([
          {
            tournamentPlayerId: `tp-${p.playerId}`,
            tournamentTeamId: team,
            tournamentId: t.id,
            tournamentName: t.name,
            teamId: TEAM_NAMES[team]!.id,
            teamName: TEAM_NAMES[team]!.name,
            groupId: IDS.group,
            shirtNumber: p.shirtNumber,
            position: p.defaultPosition,
          },
        ])
      if (params.part === "schedule") return HttpResponse.json(matches.map(childMatch))
      if (params.part === "results")
        return HttpResponse.json(matches.filter((m) => m.status === "Finished").map(childMatch))
      return HttpResponse.json([
        {
          tournamentTeamId: team,
          groupId: IDS.group,
          groupName: "Grupa A",
          tournamentName: t.name,
          teamNames: Object.fromEntries(Object.entries(TEAM_NAMES).map(([id, x]) => [id, x.name])),
          standings: Object.keys(TEAM_NAMES).map((id, i) => ({
            tournamentTeamId: id,
            position: i + 1,
            points: 0,
            matchesPlayed: 0,
            wins: 0,
            draws: 0,
            losses: 0,
            goalsFor: 0,
            goalsAgainst: 0,
          })),
        },
      ])
    }),
  ),
  http.patch(
    "*/api/parent/players/:id",
    authed<{ id: string }>(async ({ params, request, user }) => {
      const guardian = db.guardians.find((g) => g.userId === user.id && g.playerId === params.id)
      if (!guardian?.canEdit) return problem(403, "Forbidden")
      const body = (await request.json()) as { firstName: string; lastName: string }
      const p = rosterPlayer(params.id)!
      p.firstName = body.firstName
      p.lastName = body.lastName
      return HttpResponse.json({ playerId: p.playerId })
    }),
  ),

  // --- Portal publiczny (bez logowania) ---
  http.get("*/public/tournaments", () =>
    HttpResponse.json(
      db.tournaments
        .filter(
          (t) =>
            t.isPublic &&
            configOf(t.id).publicSettings.showInDirectory !== false &&
            t.status !== "Draft" &&
            t.status !== "Cancelled",
        )
        .map((t) => ({
          name: t.name,
          slug: t.slug,
          status: t.status,
          startAt: t.startAt,
          endAt: t.endAt,
          organizer: {
            id: t.organizationId,
            name: db.organizations.find((o) => o.id === t.organizationId)?.name ?? "",
          },
          categories: t.id === IDS.tournament ? ["U12"] : [],
          teamCount: tournamentTeams(t.id).length,
          liveMatchCount: db.matches.filter(
            (m) => m.tournamentId === t.id && (m.status === "Live" || m.status === "Paused"),
          ).length,
        })),
    ),
  ),
  http.get("*/public/tournaments/:slug", ({ params }) => {
    const t = db.tournaments.find((x) => x.slug === params.slug && x.isPublic)
    if (!t) return problem(404, "Tournament not found")
    return HttpResponse.json({
      id: t.id,
      name: t.name,
      slug: t.slug,
      status: t.status,
      format: t.format,
      startAt: t.startAt,
      endAt: t.endAt,
      description: t.description,
      settings: {
        showTeams: true,
        showPlayers: true,
        showSchedule: true,
        showStandings: true,
        showBracket: false,
        showLiveMatches: true,
        showStats: true,
      },
      categories: [{ id: IDS.category, name: "U12", order: 1 }],
      groups: [{ id: IDS.group, categoryId: IDS.category, name: "Grupa A", order: 1 }],
      pitches: [{ id: IDS.pitch, name: "Boisko Główne" }],
    })
  }),
  http.get("*/public/tournaments/:slug/schedule", ({ params }) => {
    const t = db.tournaments.find((x) => x.slug === params.slug && x.isPublic)
    return t
      ? HttpResponse.json(db.matches.filter((m) => m.tournamentId === t.id).map(matchDto))
      : problem(404, "Not found")
  }),
  http.get("*/public/tournaments/:slug/teams", () =>
    HttpResponse.json(
      tournamentTeams(IDS.tournament).map((t) => ({
        tournamentTeamId: t.id,
        teamId: t.teamId,
        name: t.teamName,
        shortName: t.teamShortName,
        categoryId: t.categoryId,
        groupId: t.groupId,
        logoUrl: null,
      })),
    ),
  ),
  http.get("*/public/tournaments/:slug/standings", () => HttpResponse.json([])),
  http.get("*/public/tournaments/:slug/stats/:kind", () => HttpResponse.json([])),
  http.get("*/public/matches/:id/live", ({ params }) => {
    const m = findMatch(String(params.id))
    return m
      ? HttpResponse.json({
          ...livePanel(m),
          events: m.events.map((e) => ({ ...e, createdByUserId: "" })),
        })
      : problem(404, "Not found")
  }),

  // --- Baza drużyn i kadra -----------------------------------------------------------------------
  http.get(
    "*/api/organizations/:id/teams",
    authed(({ user, params }) => {
      if (!hasRole(user, "Organizer", "Organization", params.id)) return problem(403, "Forbidden")
      return HttpResponse.json(
        [...Object.values(TEAM_NAMES).map((t) => ({ ...t, logoUrl: null })), ...db.orgTeams].map((t) => ({
          ...t,
          organizationId: params.id,
        })),
      )
    }),
  ),
  http.post(
    "*/api/organizations/:id/teams",
    authed<{ id: string }>(async ({ user, params, request }) => {
      if (!hasRole(user, "Organizer", "Organization", params.id)) return problem(403, "Forbidden")
      const body = (await request.json()) as { name: string; shortName: string | null; logoUrl: string | null }
      const team = { id: newId(), name: body.name, shortName: body.shortName, logoUrl: body.logoUrl }
      db.orgTeams.push(team)
      return HttpResponse.json({ teamId: team.id }, { status: 201 })
    }),
  ),
  http.put(
    "*/api/organizations/:id/teams/:teamId/logo",
    authed<{ id: string; teamId: string }>(async ({ user, params, request }) => {
      if (!hasRole(user, "Organizer", "Organization", params.id)) return problem(403, "Forbidden")
      const team = db.orgTeams.find((t) => t.id === params.teamId)
      if (!team) return problem(404, "Team not found")
      team.logoUrl = ((await request.json()) as { logoUrl: string | null }).logoUrl
      return new HttpResponse(null, { status: 204 })
    }),
  ),
  http.put(
    "*/api/organizations/:id/teams/:teamId",
    authed<{ id: string; teamId: string }>(async ({ user, params, request }) => {
      if (!hasRole(user, "Organizer", "Organization", params.id)) return problem(403, "Forbidden")
      const team = db.orgTeams.find((t) => t.id === params.teamId)
      if (!team) return problem(404, "Team not found")
      const body = (await request.json()) as { name: string; shortName: string | null }
      Object.assign(team, { name: body.name, shortName: body.shortName })
      return HttpResponse.json({ teamId: team.id })
    }),
  ),
  http.get(
    "*/api/teams/:teamId/players",
    authed(({ user, params }) => {
      const roster = rosterOf(user, params.teamId)
      if (!roster) return problem(403, "Forbidden")
      const team = Object.values(TEAM_NAMES).find((t) => t.id === params.teamId)!
      return HttpResponse.json({
        teamId: team.id,
        teamName: team.name,
        canEdit: true,
        players: roster.map(rosterDto),
      })
    }),
  ),
  http.post(
    "*/api/teams/:teamId/players",
    authed(async ({ user, params, request }) => {
      const roster = rosterOf(user, params.teamId)
      if (!roster) return problem(403, "Forbidden")
      const body = (await request.json()) as RosterBody
      if (numberTaken(roster, body.shirtNumber, null)) return numberTakenProblem()
      const entry: RosterEntry = {
        playerId: newId(),
        firstName: body.firstName,
        lastName: body.lastName,
        birthDate: body.birthDate,
        defaultPosition: POSITIONS[body.defaultPosition] ?? "Unknown",
        shirtNumber: body.shirtNumber,
        status: "Active",
        leftAt: null,
        inCurrentSquad: false,
        pastTournamentsCount: 0,
      }
      roster.push(entry)
      return HttpResponse.json({ playerId: entry.playerId }, { status: 201 })
    }),
  ),
  http.put(
    "*/api/teams/:teamId/players/:playerId",
    authed(async ({ user, params, request }) => {
      const roster = rosterOf(user, params.teamId)
      const entry = roster?.find((p) => p.playerId === params.playerId)
      if (!roster || !entry) return problem(404, "Player not found")
      const body = (await request.json()) as RosterBody
      if (numberTaken(roster, body.shirtNumber, entry.playerId)) return numberTakenProblem()
      Object.assign(entry, {
        firstName: body.firstName,
        lastName: body.lastName,
        birthDate: body.birthDate,
        defaultPosition: POSITIONS[body.defaultPosition] ?? "Unknown",
        shirtNumber: body.shirtNumber,
      })
      return HttpResponse.json({ playerId: entry.playerId })
    }),
  ),
  http.delete(
    "*/api/teams/:teamId/players/:playerId",
    authed(({ user, params, request }) => {
      const roster = rosterOf(user, params.teamId)
      const entry = roster?.find((p) => p.playerId === params.playerId)
      if (!roster || !entry) return problem(404, "Player not found")
      const fromSquads = new URL(request.url).searchParams.get("removeFromCurrentSquads") === "true"
      const removedFromSquads = fromSquads && entry.inCurrentSquad ? 1 : 0
      const hadHistory = entry.inCurrentSquad || entry.pastTournamentsCount > 0
      if (fromSquads) entry.inCurrentSquad = false
      // Jak backend: kto nigdzie nie grał, znika całkiem; reszta zostaje jako były zawodnik.
      const deleted = !hadHistory
      if (deleted) roster.splice(roster.indexOf(entry), 1)
      else Object.assign(entry, { status: "Left", leftAt: new Date().toISOString() })
      return HttpResponse.json({
        playerId: entry.playerId,
        deleted,
        removedFromSquads,
        keptInSquads: [],
      })
    }),
  ),
  http.post(
    "*/api/teams/:teamId/players/:playerId/restore",
    authed(({ user, params }) => {
      const roster = rosterOf(user, params.teamId)
      const entry = roster?.find((p) => p.playerId === params.playerId)
      if (!roster || !entry) return problem(404, "Player not found")
      if (numberTaken(roster, entry.shirtNumber, entry.playerId)) entry.shirtNumber = null
      Object.assign(entry, { status: "Active", leftAt: null })
      return HttpResponse.json({ playerId: entry.playerId, shirtNumber: entry.shirtNumber })
    }),
  ),

  // --- SignalR negotiate (WebSocket obsługuje signalr.ts) ---
  http.post("*/hubs/:hub/negotiate", () => HttpResponse.json(negotiateResponse())),
]

// --- Sterowanie mockiem z testów ---------------------------------------------------------------

// --- Konfiguracja turnieju (zasady, portal, kategorie, grupy, boiska, kryteria) ---------------
// Stan per turniej, reset razem z mockiem. Seedowany turniej ma kategorię U12, grupę i boisko z seeda.
type Config = {
  settings: Record<string, unknown>
  publicSettings: Record<string, boolean>
  categories: { id: string; tournamentId: string; name: string; minBirthDate: string | null; maxBirthDate: string | null; order: number }[]
  groups: { id: string; tournamentId: string; categoryId: string; name: string; order: number }[]
  pitches: { id: string; tournamentId: string; name: string; description: string | null; isActive: boolean }[]
  tieBreakers: { order: number; type: string }[]
}

const TIEBREAKERS = ["HeadToHeadPoints", "HeadToHeadGoalDiff", "GoalDifference", "GoalsFor", "Wins", "ManualDecision", "RandomDraw"]

let configs = new Map<string, Config>()

function configOf(tournamentId: string): Config {
  let c = configs.get(tournamentId)
  if (c) return c
  const seeded = tournamentId === IDS.tournament
  c = {
    settings: {
      pointsForWin: 3,
      pointsForDraw: 1,
      pointsForLoss: 0,
      matchDurationMinutes: 20,
      breakDurationMinutes: 5,
      isThirdPlaceMatchEnabled: false,
      refereeEditWindowMinutes: 15,
      walkoverHomeScore: 3,
      walkoverAwayScore: 0,
      canCoachEditSquadAfterStart: false,
    },
    publicSettings: {
      isPublic: seeded,
      showTeams: true,
      showPlayers: true,
      showSchedule: true,
      showStandings: true,
      showBracket: true,
      showLiveMatches: true,
      showStats: true,
      abbreviatePlayerNames: true,
      showInDirectory: true,
    },
    categories: seeded
      ? [{ id: IDS.category, tournamentId, name: "U12", minBirthDate: null, maxBirthDate: null, order: 1 }]
      : [],
    groups: seeded ? [{ id: IDS.group, tournamentId, categoryId: IDS.category, name: "Grupa A", order: 1 }] : [],
    pitches: seeded
      ? [{ id: IDS.pitch, tournamentId, name: "Boisko Główne", description: null, isActive: true }]
      : [],
    tieBreakers: TIEBREAKERS.slice(0, 5).map((type, i) => ({ order: i + 1, type })),
  }
  configs.set(tournamentId, c)
  return c
}

const validation = (field: string, message: string) =>
  HttpResponse.json({ code: `Validation.${field}`, message, type: 2 }, { status: 400 })

/** Tylko organizator turnieju zmienia jego konfigurację; tylko przed startem (jak backend dla zasad). */
const organizerOf = (user: User, tournamentId: string) => hasRole(user, "Organizer", "Tournament", tournamentId)

const config = [
  http.get(
    "*/api/tournaments/:id/settings",
    authed<{ id: string }>(({ params }) => HttpResponse.json(configOf(params.id).settings)),
  ),
  http.put(
    "*/api/tournaments/:id/settings",
    authed<{ id: string }>(async ({ params, request, user }) => {
      if (!organizerOf(user, params.id)) return problem(403, "Forbidden")
      const t = findTournament(params.id)
      if (t && ["InProgress", "Finished", "Cancelled"].includes(t.status))
        return validation("Settings", "Tournament settings cannot be changed after the tournament has started")
      configOf(params.id).settings = (await request.json()) as Record<string, unknown>
      return new HttpResponse(null, { status: 204 })
    }),
  ),
  http.put(
    "*/api/tournaments/:id",
    authed<{ id: string }>(async ({ params, request, user }) => {
      const t = findTournament(params.id)
      if (!t) return problem(404, "Tournament not found")
      if (!organizerOf(user, params.id)) return problem(403, "Forbidden")
      const body = (await request.json()) as Pick<Tournament, "name" | "startAt" | "endAt" | "teamLimit" | "description">
      Object.assign(t, {
        name: body.name,
        startAt: body.startAt,
        endAt: body.endAt,
        teamLimit: body.teamLimit,
        description: body.description,
      })
      return new HttpResponse(null, { status: 204 })
    }),
  ),
  http.get("*/api/tournaments/:id/members", authed(() => HttpResponse.json([]))),
  http.get(
    "*/api/tournaments/:id/public-settings",
    authed<{ id: string }>(({ params }) => HttpResponse.json(configOf(params.id).publicSettings)),
  ),
  http.put(
    "*/api/tournaments/:id/public-settings",
    authed<{ id: string }>(async ({ params, request, user }) => {
      if (!organizerOf(user, params.id)) return problem(403, "Forbidden")
      const c = configOf(params.id)
      c.publicSettings = { ...c.publicSettings, ...((await request.json()) as Record<string, boolean>) }
      const t = findTournament(params.id)
      if (t) t.isPublic = !!c.publicSettings.isPublic
      return new HttpResponse(null, { status: 204 })
    }),
  ),
  http.patch(
    "*/api/tournaments/:id/format",
    authed<{ id: string }>(async ({ params, request, user }) => {
      const t = findTournament(params.id)
      if (!t) return problem(404, "Tournament not found")
      if (!organizerOf(user, params.id)) return problem(403, "Forbidden")
      if (!["Draft", "RegistrationOpen", "RegistrationClosed"].includes(t.status))
        return validation("TournamentFormat", "Tournament format cannot be changed after the schedule is generated")
      const { tournamentFormat } = (await request.json()) as { tournamentFormat: number | string }
      t.format = typeof tournamentFormat === "string" ? tournamentFormat : ["Group", "Knockout", "GroupAndKnockout"][tournamentFormat - 1]!
      return new HttpResponse(null, { status: 204 })
    }),
  ),
  http.get(
    "*/api/tournaments/:id/tiebreaker-rules",
    authed<{ id: string }>(({ params }) => HttpResponse.json(configOf(params.id).tieBreakers)),
  ),
  http.put(
    "*/api/tournaments/:id/tiebreaker-rules",
    authed<{ id: string }>(async ({ params, request, user }) => {
      if (!organizerOf(user, params.id)) return problem(403, "Forbidden")
      const { rules } = (await request.json()) as { rules: { order: number; type: number | string }[] }
      configOf(params.id).tieBreakers = rules.map((r) => ({
        order: r.order,
        type: typeof r.type === "string" ? r.type : TIEBREAKERS[r.type - 1]!,
      }))
      return new HttpResponse(null, { status: 204 })
    }),
  ),
  http.get(
    "*/api/tournaments/:id/categories",
    authed<{ id: string }>(({ params }) => HttpResponse.json(configOf(params.id).categories)),
  ),
  http.post(
    "*/api/tournaments/:id/categories",
    authed<{ id: string }>(async ({ params, request, user }) => {
      if (!organizerOf(user, params.id)) return problem(403, "Forbidden")
      const c = configOf(params.id)
      const body = (await request.json()) as { name: string; minBirthDate: string | null; maxBirthDate: string | null; order: number; groupCount?: number }
      if (c.categories.some((x) => x.name.toLowerCase() === body.name.trim().toLowerCase()))
        return validation("Name", `Tournament category with name '${body.name}' already exists`)
      const category = { id: newId(), tournamentId: params.id, name: body.name.trim(), minBirthDate: body.minBirthDate, maxBirthDate: body.maxBirthDate, order: body.order }
      c.categories.push(category)
      // Jak backend: przy formacie z grupami nowa kategoria dostaje od razu "Grupę A".
      const format = findTournament(params.id)?.format
      if (format === "Group" || format === "GroupAndKnockout")
        for (let i = 0; i < (body.groupCount ?? 1); i++)
          c.groups.push({ id: newId(), tournamentId: params.id, categoryId: category.id, name: `Grupa ${String.fromCharCode(65 + i)}`, order: i + 1 })
      return HttpResponse.json({ tournamentCategoryId: category.id }, { status: 201 })
    }),
  ),
  http.delete(
    "*/api/tournaments/:id/categories/:categoryId",
    authed<{ id: string; categoryId: string }>(({ params, user }) => {
      if (!organizerOf(user, params.id)) return problem(403, "Forbidden")
      const c = configOf(params.id)
      c.categories = c.categories.filter((x) => x.id !== params.categoryId)
      c.groups = c.groups.filter((g) => g.categoryId !== params.categoryId)
      return new HttpResponse(null, { status: 204 })
    }),
  ),
  http.get(
    "*/api/tournaments/:id/standings",
    authed<{ id: string }>(({ params, request }) =>
      HttpResponse.json(groupStandings(params.id, new URL(request.url).searchParams.get("groupId") ?? "")),
    ),
  ),
  http.get(
    "*/api/tournaments/:id/groups",
    authed<{ id: string }>(({ params }) => HttpResponse.json(configOf(params.id).groups)),
  ),
  http.post(
    "*/api/tournaments/:id/groups",
    authed<{ id: string }>(async ({ params, request, user }) => {
      if (!organizerOf(user, params.id)) return problem(403, "Forbidden")
      const c = configOf(params.id)
      const body = (await request.json()) as { categoryId: string; name: string; order: number }
      if (c.groups.some((g) => g.categoryId === body.categoryId && g.name.toLowerCase() === body.name.trim().toLowerCase()))
        return validation("Name", "Name already exists")
      const group = { id: newId(), tournamentId: params.id, categoryId: body.categoryId, name: body.name.trim(), order: body.order }
      c.groups.push(group)
      return HttpResponse.json({ tournamentGroupId: group.id }, { status: 201 })
    }),
  ),
  http.put(
    "*/api/tournaments/:id/groups/:groupId",
    authed<{ id: string; groupId: string }>(async ({ params, request, user }) => {
      if (!organizerOf(user, params.id)) return problem(403, "Forbidden")
      const group = configOf(params.id).groups.find((g) => g.id === params.groupId)
      if (!group) return problem(404, "Not found")
      const body = (await request.json()) as { name: string; order: number }
      Object.assign(group, { name: body.name.trim(), order: body.order })
      return HttpResponse.json({ tournamentGroupId: group.id })
    }),
  ),
  http.delete(
    "*/api/tournaments/:id/groups/:groupId",
    authed<{ id: string; groupId: string }>(({ params, user }) => {
      if (!organizerOf(user, params.id)) return problem(403, "Forbidden")
      // Jak backend: blokują tylko oczekujące i przyjęte drużyny oraz mecze grupy.
      if (tournamentTeams(params.id).some((t) => t.groupId === params.groupId && (t.registrationStatus === "Pending" || t.registrationStatus === "Accepted")))
        return problem(409, "Cannot delete a group that still has teams assigned to it")
      if (db.matches.some((m) => m.groupId === params.groupId))
        return problem(409, "Cannot delete a group that still has matches scheduled in it")
      const c = configOf(params.id)
      c.groups = c.groups.filter((g) => g.id !== params.groupId)
      return new HttpResponse(null, { status: 204 })
    }),
  ),
  http.get(
    "*/api/tournaments/:id/pitches",
    authed<{ id: string }>(({ params }) => HttpResponse.json(configOf(params.id).pitches)),
  ),
  http.post(
    "*/api/tournaments/:id/pitches",
    authed<{ id: string }>(async ({ params, request, user }) => {
      if (!organizerOf(user, params.id)) return problem(403, "Forbidden")
      const c = configOf(params.id)
      const body = (await request.json()) as { name: string; description: string | null }
      if (c.pitches.some((p) => p.name.toLowerCase() === body.name.trim().toLowerCase()))
        return validation("Name", `Pitch with name '${body.name}' already exists in this tournament`)
      const pitch = { id: newId(), tournamentId: params.id, name: body.name.trim(), description: body.description, isActive: true }
      c.pitches.push(pitch)
      return HttpResponse.json({ pitchId: pitch.id }, { status: 201 })
    }),
  ),
  http.get(
    "*/api/tournaments/:id/schedule",
    authed<{ id: string }>(({ params }) =>
      HttpResponse.json(db.matches.filter((m) => m.tournamentId === params.id).map(matchDto)),
    ),
  ),
]

// --- Skład drużyny w turnieju (reguły jak w Players: wiek kategorii, numer, dubel, import) -------
type SquadEntry = {
  id: string
  tournamentTeamId: string
  playerId: string
  shirtNumber: number | null
  position: string | null
  status: string
  firstName: string
  lastName: string
  birthDate: string | null
  photoUrl: string | null
}

const PLAYER_STATUSES = ["Active", "Injured", "Suspended", "Removed"]
const SQUAD_POSITIONS = ["Goalkeeper", "Defender", "Midfielder", "Forward"]
const enumName = (values: string[], v: unknown) =>
  v == null ? null : typeof v === "number" ? (values[v - 1] ?? null) : String(v)

let squads = new Map<string, SquadEntry[]>()

function squadEntries(tournamentTeamId: string): SquadEntry[] {
  let squad = squads.get(tournamentTeamId)
  if (squad) return squad
  squad = (db.rosters[TEAM_NAMES[tournamentTeamId]?.id ?? ""] ?? [])
    .filter((p) => p.inCurrentSquad)
    .map((p) => ({
      id: `tp-${p.playerId}`,
      tournamentTeamId,
      playerId: p.playerId,
      shirtNumber: p.shirtNumber,
      position: p.defaultPosition,
      status: "Active",
      firstName: p.firstName,
      lastName: p.lastName,
      birthDate: p.birthDate,
      photoUrl: null,
    }))
  squads.set(tournamentTeamId, squad)
  return squad
}

/** Wiek względem kategorii zgłoszenia (seed: obie drużyny w kategorii seedowanego turnieju). */
function ageError(tournamentId: string, birthDate: string | null) {
  const category = configOf(tournamentId).categories.find((c) => c.id === IDS.category)
  if (!category || !birthDate) return null
  const born = birthDate.slice(0, 10)
  if (category.minBirthDate && born < category.minBirthDate.slice(0, 10))
    return validation("BirthDate", "Player is too old for this category")
  if (category.maxBirthDate && born > category.maxBirthDate.slice(0, 10))
    return validation("BirthDate", "Player is too young for this category")
  return null
}

const shirtTaken = (squad: SquadEntry[], shirt: number | null, exceptId?: string) =>
  shirt != null && squad.some((p) => p.id !== exceptId && p.status !== "Removed" && p.shirtNumber === shirt)

const squadBase = "*/api/tournaments/:tid/tournament-teams/:ttid"

const squad = [
  http.patch(
    `${squadBase}/group`,
    authed<{ tid: string; ttid: string }>(async ({ params, request, user }) => {
      if (!organizerOf(user, params.tid)) return problem(403, "Forbidden")
      const body = (await request.json()) as { groupId: string | null }
      db.teamChanges[params.ttid] = { ...db.teamChanges[params.ttid], groupId: body.groupId }
      return new HttpResponse(null, { status: 204 })
    }),
  ),
  http.patch(
    `${squadBase}/registration-status`,
    authed<{ tid: string; ttid: string }>(async ({ params, request, user }) => {
      if (!organizerOf(user, params.tid)) return problem(403, "Forbidden")
      const body = (await request.json()) as { registrationStatus: number }
      const status = ["", "Pending", "Accepted", "Rejected", "Withdrawn"][body.registrationStatus]
      db.teamChanges[params.ttid] = { ...db.teamChanges[params.ttid], registrationStatus: status }
      return new HttpResponse(null, { status: 204 })
    }),
  ),
  http.get(
    `${squadBase}/players`,
    authed<{ tid: string; ttid: string }>(({ params }) => HttpResponse.json(squadEntries(params.ttid))),
  ),
  http.get(`${squadBase}/staff`, authed(() => HttpResponse.json([]))),
  http.get(
    `${squadBase}/roster-sources`,
    authed<{ tid: string; ttid: string }>(({ params }) => {
      const inSquad = new Set(squadEntries(params.ttid).map((p) => p.playerId))
      const roster = db.rosters[TEAM_NAMES[params.ttid]?.id ?? ""] ?? []
      return HttpResponse.json([
        {
          tournamentTeamId: null,
          tournamentId: null,
          tournamentName: "Kadra drużyny",
          tournamentStartAt: null,
          categoryName: null,
          kind: "Roster",
          players: roster
            .filter((p) => p.status === "Active")
            .map((p) => ({
              playerId: p.playerId,
              firstName: p.firstName,
              lastName: p.lastName,
              birthDate: p.birthDate,
              shirtNumber: p.shirtNumber,
              position: p.defaultPosition,
              alreadyInSquad: inSquad.has(p.playerId),
              ineligibility: null,
            })),
        },
      ])
    }),
  ),
  http.post(
    `${squadBase}/players/new`,
    authed<{ tid: string; ttid: string }>(async ({ params, request, user }) => {
      if (!organizerOf(user, params.tid) && !hasRole(user, "Coach", "TournamentTeam", params.ttid))
        return problem(403, "Forbidden")
      const body = (await request.json()) as {
        firstName: string
        lastName: string
        birthDate: string
        defaultPosition: number | string
        shirtNumber: number | null
        position: number | string | null
        allowDuplicate?: boolean
      }
      const entries = squadEntries(params.ttid)
      const age = ageError(params.tid, body.birthDate)
      if (age) return age
      const duplicate = entries.some(
        (p) =>
          p.firstName.toLowerCase() === body.firstName.trim().toLowerCase() &&
          p.lastName.toLowerCase() === body.lastName.trim().toLowerCase() &&
          p.birthDate?.slice(0, 10) === body.birthDate.slice(0, 10),
      )
      if (duplicate && !body.allowDuplicate)
        return validation("LastName", "A player with the same name and birth date is already in this team")
      if (shirtTaken(entries, body.shirtNumber))
        return validation("ShirtNumber", "Shirt number is already taken in this team")
      const entry: SquadEntry = {
        id: newId(),
        tournamentTeamId: params.ttid,
        playerId: newId(),
        shirtNumber: body.shirtNumber,
        position: enumName(SQUAD_POSITIONS, body.position ?? body.defaultPosition),
        status: "Active",
        firstName: body.firstName.trim(),
        lastName: body.lastName.trim(),
        birthDate: body.birthDate,
        photoUrl: null,
      }
      entries.push(entry)
      return HttpResponse.json({ playerId: entry.playerId, tournamentPlayerId: entry.id }, { status: 201 })
    }),
  ),
  http.put(
    `${squadBase}/players/:tpid`,
    authed<{ tid: string; ttid: string; tpid: string }>(async ({ params, request }) => {
      const entries = squadEntries(params.ttid)
      const entry = entries.find((p) => p.id === params.tpid)
      if (!entry) return problem(404, "Player not found")
      const body = (await request.json()) as {
        firstName: string
        lastName: string
        birthDate: string
        shirtNumber: number | null
        position: number | string | null
        status: number | string
      }
      const age = ageError(params.tid, body.birthDate)
      if (age) return age
      if (shirtTaken(entries, body.shirtNumber, entry.id))
        return validation("ShirtNumber", "Shirt number is already taken in this team")
      Object.assign(entry, {
        firstName: body.firstName,
        lastName: body.lastName,
        birthDate: body.birthDate,
        shirtNumber: body.shirtNumber,
        position: enumName(SQUAD_POSITIONS, body.position),
        status: enumName(PLAYER_STATUSES, body.status) ?? entry.status,
      })
      return HttpResponse.json({ playerId: entry.playerId, tournamentPlayerId: entry.id })
    }),
  ),
  http.delete(
    `${squadBase}/players/:tpid`,
    authed<{ tid: string; ttid: string; tpid: string }>(({ params }) => {
      squads.set(params.ttid, squadEntries(params.ttid).filter((p) => p.id !== params.tpid))
      return HttpResponse.json({ tournamentPlayerId: params.tpid, deleted: true })
    }),
  ),
  http.post(
    `${squadBase}/players/import`,
    authed<{ tid: string; ttid: string }>(async ({ params, request }) => {
      const entries = squadEntries(params.ttid)
      const body = (await request.json()) as {
        players: { playerId: string; shirtNumber: number | null; position: number | string | null }[]
      }
      const roster = db.rosters[TEAM_NAMES[params.ttid]?.id ?? ""] ?? []
      const added: SquadEntry[] = []
      for (const item of body.players) {
        if ([...entries, ...added].some((p) => p.playerId === item.playerId))
          return validation("Players", "Player is already assigned to this team")
        if (shirtTaken([...entries, ...added], item.shirtNumber))
          return validation("Players", "Shirt number is already taken in this team")
        const source = roster.find((p) => p.playerId === item.playerId)
        if (!source) return validation("Players", "Player is not part of the source squad")
        added.push({
          id: newId(),
          tournamentTeamId: params.ttid,
          playerId: source.playerId,
          shirtNumber: item.shirtNumber,
          position: enumName(SQUAD_POSITIONS, item.position) ?? source.defaultPosition,
          status: "Active",
          firstName: source.firstName,
          lastName: source.lastName,
          birthDate: source.birthDate,
          photoUrl: null,
        })
      }
      entries.push(...added)
      return HttpResponse.json({ tournamentPlayerIds: added.map((p) => p.id) })
    }),
  ),
]

// --- Drabinka (pary 1. rundy, kolejne rundy ze zwycięzców, wolny los przechodzi od razu) --------
type Node = {
  id: string
  bracketRoundId: string
  matchId: string | null
  homeSourceType: string
  homeTournamentTeamId: string | null
  awaySourceType: string
  awayTournamentTeamId: string | null
  winnerGoesToNodeId: string | null
  loserGoesToNodeId: string | null
  winnerGoesToSlot: "Home" | "Away" | null
  homeSourceGroupId: string | null
  homeSourcePosition: number | null
  awaySourceGroupId: string | null
  awaySourcePosition: number | null
  plannedPitchId: string | null
  plannedAt: string | null
  plannedRefereeId: string | null
}
type Round = { id: string; roundNumber: number; roundType: string; name: string; nodes: Node[] }

const SOURCE_TYPES = ["Fixed", "GroupPosition", "MatchWinner", "MatchLoser", "Bye"]
const ROUND_NAMES: Record<number, string> = { 1: "Finał", 2: "Półfinały", 4: "Ćwierćfinały", 8: "1/8 finału" }

let brackets = new Map<string, { id: string; tournamentId: string; rounds: Round[] }>()

const emptyNode = (roundId: string): Node => ({
  id: newId(),
  bracketRoundId: roundId,
  matchId: null,
  homeSourceType: "MatchWinner",
  homeTournamentTeamId: null,
  awaySourceType: "MatchWinner",
  awayTournamentTeamId: null,
  winnerGoesToNodeId: null,
  loserGoesToNodeId: null,
  winnerGoesToSlot: null,
  homeSourceGroupId: null,
  homeSourcePosition: null,
  awaySourceGroupId: null,
  awaySourcePosition: null,
  plannedPitchId: null,
  plannedAt: null,
  plannedRefereeId: null,
})

const bracketKey = (tournamentId: string, categoryId: string) => `${tournamentId}:${categoryId}`

const bracket = [
  http.get(
    "*/api/tournaments/:id/bracket",
    authed<{ id: string }>(({ params, request }) => {
      const categoryId = new URL(request.url).searchParams.get("categoryId") ?? ""
      const b = brackets.get(bracketKey(params.id, categoryId))
      return b ? HttpResponse.json(b) : problem(404, "Bracket not found")
    }),
  ),
  http.post(
    "*/api/tournaments/:id/bracket/generate",
    authed<{ id: string }>(async ({ params, request, user }) => {
      if (!organizerOf(user, params.id)) return problem(403, "Forbidden")
      const body = (await request.json()) as {
        categoryId: string
        roundOnePairings: Record<string, string | number | null>[]
      }
      if (brackets.has(bracketKey(params.id, body.categoryId)))
        return validation("CategoryId", "A bracket already exists for this tournament")
      const pairs = body.roundOnePairings
      const source = (v: unknown) => (typeof v === "number" ? SOURCE_TYPES[v - 1]! : String(v))
      if (pairs.some((p) => source(p.homeSourceType) === "Bye" && source(p.awaySourceType) === "Bye"))
        return validation("RoundOnePairings", "A pairing cannot have Bye on both sides — there would be no team to advance.")

      const rounds: Round[] = []
      for (let size = pairs.length, number = 1; size >= 1; size /= 2, number++) {
        const id = newId()
        rounds.push({ id, roundNumber: number, roundType: "Standard", name: ROUND_NAMES[size] ?? `Runda ${number}`, nodes: Array.from({ length: size }, () => emptyNode(id)) })
      }
      rounds[0]!.nodes.forEach((node, i) => {
        const p = pairs[i]!
        Object.assign(node, {
          homeSourceType: source(p.homeSourceType),
          homeTournamentTeamId: p.homeTournamentTeamId ?? null,
          homeSourceGroupId: p.homeGroupId ?? null,
          homeSourcePosition: p.homePosition ?? null,
          awaySourceType: source(p.awaySourceType),
          awayTournamentTeamId: p.awayTournamentTeamId ?? null,
          awaySourceGroupId: p.awayGroupId ?? null,
          awaySourcePosition: p.awayPosition ?? null,
        })
      })
      for (let r = 0; r < rounds.length - 1; r++) {
        rounds[r]!.nodes.forEach((node, i) => {
          const next = rounds[r + 1]!.nodes[Math.floor(i / 2)]!
          node.winnerGoesToNodeId = next.id
          node.winnerGoesToSlot = i % 2 === 0 ? "Home" : "Away"
          // Wolny los: drużyna od razu w kolejnej rundzie.
          if (r === 0 && (node.homeSourceType === "Bye" || node.awaySourceType === "Bye")) {
            const team = node.homeSourceType === "Bye" ? node.awayTournamentTeamId : node.homeTournamentTeamId
            if (i % 2 === 0) Object.assign(next, { homeSourceType: "Fixed", homeTournamentTeamId: team })
            else Object.assign(next, { awaySourceType: "Fixed", awayTournamentTeamId: team })
          }
        })
      }
      if (configOf(params.id).settings.isThirdPlaceMatchEnabled && pairs.length >= 2) {
        const id = newId()
        const third = emptyNode(id)
        third.homeSourceType = "MatchLoser"
        third.awaySourceType = "MatchLoser"
        rounds[rounds.length - 2]!.nodes.forEach((n) => (n.loserGoesToNodeId = third.id))
        rounds.push({ id, roundNumber: rounds.length, roundType: "ThirdPlace", name: "Mecz o 3. miejsce", nodes: [third] })
      }
      const created = { id: newId(), tournamentId: params.id, rounds }
      brackets.set(bracketKey(params.id, body.categoryId), created)
      return HttpResponse.json({ bracketId: created.id, roundCount: rounds.length }, { status: 201 })
    }),
  ),
  http.delete(
    "*/api/tournaments/:id/bracket",
    authed<{ id: string }>(({ params, request, user }) => {
      if (!organizerOf(user, params.id)) return problem(403, "Forbidden")
      const categoryId = new URL(request.url).searchParams.get("categoryId") ?? ""
      brackets.delete(bracketKey(params.id, categoryId))
      return new HttpResponse(null, { status: 204 })
    }),
  ),
]

const control = [
  http.post("*/__control/override", async ({ request }) => {
    const o = (await request.json()) as { method: string; path: string; status?: number; body?: unknown; times?: number }
    overrides.push({
      method: o.method.toUpperCase(),
      pattern: toPattern(o.path),
      status: o.status ?? 200,
      body: o.body ?? null,
      times: o.times ?? null,
    })
    return new HttpResponse(null, { status: 204 })
  }),
  // Mecz w danym stanie (np. Scheduled - do testu startu), bez zdarzeń i wyniku.
  http.post("*/__control/matches/:id/status", async ({ params, request }) => {
    const m = findMatch(String(params.id))
    if (!m) return problem(404, "Match not found")
    const { status, editLockedAt } = (await request.json()) as { status: string; editLockedAt?: string | null }
    m.status = status
    if (editLockedAt !== undefined) m.editLockedAt = editLockedAt
    // Mecz zakończony "na innym urządzeniu": tabela przeliczona jak w backendzie.
    if (status === "Finished") standingChanged(m)
    if (status === "Scheduled") {
      Object.assign(m, { startedAt: null, pausedAt: null, totalPausedSeconds: 0, homeScore: 0, awayScore: 0, events: [] })
    }
    return new HttpResponse(null, { status: 204 })
  }),
  // Pola turnieju ustawione przez test (np. limit drużyn, status, format).
  http.post("*/__control/tournaments/:id", async ({ params, request }) => {
    const t = findTournament(String(params.id))
    if (!t) return problem(404, "Tournament not found")
    Object.assign(t, (await request.json()) as Partial<Tournament>)
    return new HttpResponse(null, { status: 204 })
  }),
  // Ostatnia wersja z okna „Co nowego” ustawiona przez test (np. konto sprzed kilku wydań).
  http.post("*/__control/users/:email/seen-release", async ({ params, request }) => {
    const user = db.users.find((u) => u.email === String(params.email))
    if (!user) return problem(404, "User not found")
    user.seenReleaseVersion = ((await request.json()) as { version: string | null }).version
    return new HttpResponse(null, { status: 204 })
  }),
  // Motyw na koncie ustawiony przez test (np. wybrany wcześniej na innym urządzeniu).
  http.post("*/__control/users/:email/theme", async ({ params, request }) => {
    const user = db.users.find((u) => u.email === String(params.email))
    if (!user) return problem(404, "User not found")
    user.theme = ((await request.json()) as { theme: User["theme"] }).theme
    return new HttpResponse(null, { status: 204 })
  }),
  http.post("*/__control/reset", () => {
    overrides = []
    requests = []
    configs = new Map()
    squads = new Map()
    brackets = new Map()
    reset()
    closeAll()
    return new HttpResponse(null, { status: 204 })
  }),
  // Gol "z innego urządzenia" (np. sędzia) — portal kibica powinien go zobaczyć na żywo.
  http.post("*/__control/matches/:id/goal", async ({ params, request }) => {
    const m = findMatch(String(params.id))
    if (!m) return problem(404, "Match not found")
    const { side } = (await request.json()) as { side: "home" | "away" }
    scoreGoal(m, side === "home" ? m.home : m.away, IDS.referee, 10)
    return HttpResponse.json({ homeScore: m.homeScore, awayScore: m.awayScore })
  }),
  // Sędzia oddaje mecz "z innego urządzenia" (albo organizator go zdejmuje) — inni sędziowie
  // powinni od razu zobaczyć mecz w wolnych.
  http.post("*/__control/matches/:id/release", ({ params }) => {
    const m = findMatch(String(params.id))
    if (!m) return problem(404, "Match not found")
    releaseReferee(m)
    return new HttpResponse(null, { status: 204 })
  }),
  http.get("*/__control/state", () =>
    HttpResponse.json({
      tournaments: db.tournaments.map((t) => ({ id: t.id, name: t.name, status: t.status })),
      matches: db.matches.map((m) => ({
        id: m.id,
        homeScore: m.homeScore,
        awayScore: m.awayScore,
      })),
      users: db.users.map((u) => ({
        email: u.email,
        roles: u.roles.map((r) => `${r.role}@${r.scopeType}`),
        seenReleaseVersion: u.seenReleaseVersion ?? null,
        theme: u.theme ?? "system",
      })),
      organizations: db.organizations.map((o) => o.name),
      requests,
      connections: {
        tournaments: connectionCount("tournaments"),
        matches: connectionCount("matches"),
      },
    }),
  ),
]

export const handlers = [...control, overrideHandler, ...keycloak, ...config, ...squad, ...bracket, ...api]
