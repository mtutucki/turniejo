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

function toPattern(path: string) {
  const escaped = path.replace(/[.*+?^${}()|[\]\\]/g, "\\$&").replace(/:[A-Za-z]+/g, "[^/]+")
  return new RegExp(`${escaped}$`)
}

const overrideHandler = http.all("*", ({ request }) => {
  const url = new URL(request.url)
  const hit = overrides.find((o) => o.method === request.method && o.pattern.test(url.pathname))
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
  resultType: m.status === "Finished" ? "Regular" : "NotPlayed",
  homePenaltyScore: null,
  awayPenaltyScore: null,
})

const elapsed = (m: Match) =>
  m.startedAt ? Math.max(0, Math.floor((Date.now() - new Date(m.startedAt).getTime()) / 1000)) : 0

const matchTeam = (tournamentTeamId: string) => ({
  tournamentTeamId,
  teamId: TEAM_NAMES[tournamentTeamId]!.id,
  name: TEAM_NAMES[tournamentTeamId]!.name,
  shortName: TEAM_NAMES[tournamentTeamId]!.shortName,
  players: [],
})

const livePanel = (m: Match) => ({
  matchId: m.id,
  tournamentId: m.tournamentId,
  status: m.status,
  stageType: "Group",
  scheduledAt: m.scheduledAt,
  startedAt: m.startedAt,
  pausedAt: null,
  finishedAt: null,
  totalPausedSeconds: 0,
  homeScore: m.homeScore,
  awayScore: m.awayScore,
  resultType: "NotPlayed",
  winnerTournamentTeamId: null,
  homeTeam: matchTeam(m.home),
  awayTeam: matchTeam(m.away),
  events: m.events,
  editLockedAt: null,
  elapsedSeconds: elapsed(m),
  homePenaltyScore: null,
  awayPenaltyScore: null,
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
      }))

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

  // --- Portal publiczny (bez logowania) ---
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
        Object.values(TEAM_NAMES).map((t) => ({ ...t, organizationId: params.id, logoUrl: null })),
      )
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
  http.post("*/__control/reset", () => {
    overrides = []
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
      })),
      organizations: db.organizations.map((o) => o.name),
      connections: {
        tournaments: connectionCount("tournaments"),
        matches: connectionCount("matches"),
      },
    }),
  ),
]

export const handlers = [...control, overrideHandler, ...keycloak, ...api]
