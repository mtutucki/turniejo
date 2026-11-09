// Dane do testów pełnego stosu zakładane przez prawdziwe API (jak w TournamentScenario z testów
// integracyjnych). UI sprawdza tylko to, co jest celem testu - przygotowanie idzie skrótem.
export const APP_URL = "http://localhost:13100"
export const API_URL = "http://localhost:18081"
export const KEYCLOAK_URL = "http://localhost:18080"
export const PASSWORD = "Haslo1234!"

const run = Date.now().toString(36)
let counter = 0
/** Nowy adres w domenie testowej (qa+…@test.pl), unikalny w obrębie przebiegu. */
export const email = (role: string) => `qa+${role}-${run}-${++counter}@test.pl`

type Json = Record<string, unknown>

export class ApiClient {
  constructor(
    readonly email: string,
    private token: string,
  ) {}

  /** Id użytkownika z tokenu (claim "sub"). */
  get userId(): string {
    return JSON.parse(Buffer.from(this.token.split(".")[1]!, "base64url").toString()).sub
  }

  static async login(userEmail: string, password = PASSWORD) {
    const response = await fetch(`${KEYCLOAK_URL}/realms/turniejo/protocol/openid-connect/token`, {
      method: "POST",
      headers: { "Content-Type": "application/x-www-form-urlencoded" },
      body: new URLSearchParams({
        grant_type: "password",
        client_id: "turniejo-frontend",
        username: userEmail,
        password,
        scope: "openid",
      }),
    })
    if (!response.ok) throw new Error(`Logowanie ${userEmail}: ${response.status} ${await response.text()}`)
    return new ApiClient(userEmail, ((await response.json()) as { access_token: string }).access_token)
  }

  async request<T = Json>(method: string, path: string, body?: unknown): Promise<T> {
    const response = await fetch(`${API_URL}${path}`, {
      method,
      headers: {
        Authorization: `Bearer ${this.token}`,
        ...(body === undefined ? {} : { "Content-Type": "application/json" }),
      },
      body: body === undefined ? undefined : JSON.stringify(body),
    })
    const text = await response.text()
    if (!response.ok) throw new Error(`${method} ${path}: ${response.status} ${text}`)
    return (text ? JSON.parse(text) : undefined) as T
  }

  get = <T = Json>(path: string) => this.request<T>("GET", path)
  post = <T = Json>(path: string, body?: unknown) => this.request<T>("POST", path, body ?? {})
  put = <T = Json>(path: string, body: unknown) => this.request<T>("PUT", path, body)
  patch = <T = Json>(path: string, body: unknown) => this.request<T>("PATCH", path, body)
}

/** Konto przez publiczną rejestrację (jak w aplikacji), od razu zalogowane w API. */
export async function register(role: string, organizer = false) {
  const address = email(role)
  const response = await fetch(`${API_URL}/api/auth/register`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({
      email: address,
      password: PASSWORD,
      firstName: role[0]!.toUpperCase() + role.slice(1),
      lastName: "Testowy",
      createOrganization: organizer,
      acceptTerms: true,
    }),
  })
  if (!response.ok) throw new Error(`Rejestracja ${address}: ${response.status} ${await response.text()}`)
  return ApiClient.login(address)
}

async function poll<T>(what: string, fn: () => Promise<T | undefined>, timeoutMs = 20_000): Promise<T> {
  const until = Date.now() + timeoutMs
  for (;;) {
    try {
      const value = await fn()
      if (value !== undefined) return value
    } catch {
      // jeszcze nie gotowe
    }
    if (Date.now() > until) throw new Error(`Nie doczekano się: ${what}`)
    await new Promise((r) => setTimeout(r, 300))
  }
}

export type Match = {
  id: string
  homeTournamentTeamId: string
  awayTournamentTeamId: string
  status: string
  stageType: string
  scheduledAt: string
  homeScore: number
  awayScore: number
}

const STATUS = { RegistrationOpen: 2, RegistrationClosed: 3, InProgress: 5, Finished: 6 } as const
const FORMAT = { Group: 1, Knockout: 2, GroupAndKnockout: 3 } as const

/** Gotowy turniej: organizator, drużyny T1…Tn, grupy albo drabinka, boisko, terminarz, start. */
export class Scenario {
  readonly teams = new Map<string, string>()
  readonly groups = new Map<string, string>()
  tournamentId = ""
  categoryId = ""
  pitchId = ""
  slug = ""
  startsAt = new Date(Date.now() + 3_600_000)

  private constructor(
    readonly organizer: ApiClient,
    readonly organizationId: string,
  ) {}

  get base() {
    return `/api/tournaments/${this.tournamentId}`
  }

  team(name: string) {
    return this.teams.get(name)!
  }

  private static async create(format: keyof typeof FORMAT) {
    const organizer = await register("org", true)
    const organization = await poll("organizacja", () =>
      organizer.get<{ id: string }>("/api/organizations/mine"),
    )
    const s = new Scenario(organizer, organization.id)
    const created = await organizer.post<{ tournamentId: string; slug: string }>("/api/tournaments", {
      organizationId: organization.id,
      name: `Puchar QA ${run}-${++counter}`,
      startAt: s.startsAt.toISOString(),
      endAt: new Date(s.startsAt.getTime() + 86_400_000).toISOString(),
      teamLimit: 32,
      description: null,
    })
    s.tournamentId = created.tournamentId
    s.slug = created.slug
    await organizer.patch(`${s.base}/format`, { tournamentFormat: FORMAT[format] })
    s.categoryId = (
      await organizer.post<{ tournamentCategoryId: string }>(`${s.base}/categories`, {
        name: "Open",
        minBirthDate: null,
        maxBirthDate: null,
        order: 1,
      })
    ).tournamentCategoryId
    s.pitchId = (await organizer.post<{ pitchId: string }>(`${s.base}/pitches`, { name: "Boisko 1", description: null })).pitchId
    await s.status("RegistrationOpen")
    await organizer.put(`${s.base}/public-settings`, {
      isPublic: true, showTeams: true, showPlayers: true, showSchedule: true, showStandings: true,
      showBracket: true, showLiveMatches: true, showStats: true, abbreviatePlayerNames: true,
    })
    return s
  }

  status(status: keyof typeof STATUS) {
    return this.organizer.patch(`${this.base}/status`, { tournamentStatus: STATUS[status] })
  }

  async addTeam(name: string) {
    const { teamId } = await this.organizer.post<{ teamId: string }>(
      `/api/organizations/${this.organizationId}/teams`,
      { name, shortName: name.slice(0, 3).toUpperCase(), logoUrl: null },
    )
    const { tournamentTeamId } = await this.organizer.post<{ tournamentTeamId: string }>(`${this.base}/teams`, {
      teamId,
      categoryId: this.categoryId,
    })
    await this.organizer.patch(`${this.base}/tournament-teams/${tournamentTeamId}/registration-status`, {
      registrationStatus: 2,
    })
    this.teams.set(name, tournamentTeamId)
    return tournamentTeamId
  }

  generateSchedule() {
    return this.organizer.post(`${this.base}/schedule/generate`, {
      startAt: this.startsAt.toISOString(),
      endAt: new Date(this.startsAt.getTime() + 12 * 3_600_000).toISOString(),
      pitchIds: [this.pitchId],
    })
  }

  /** Grupy "A", "B"… po `perGroup` drużyn (T1…Tn kolejno), terminarz i start. */
  static async groups(groupCount: number, perGroup: number, format: "Group" | "GroupAndKnockout" = "Group") {
    const s = await Scenario.create(format)
    const existing = await s.organizer.get<{ id: string; categoryId: string }[]>(`${s.base}/groups`)
    let n = 1
    for (let g = 0; g < groupCount; g++) {
      const name = String.fromCharCode(65 + g)
      const groupId =
        g === 0
          ? existing.find((x) => x.categoryId === s.categoryId)!.id
          : (await s.organizer.post<{ tournamentGroupId: string }>(`${s.base}/groups`, {
              categoryId: s.categoryId,
              name: `Grupa ${name}`,
              order: g + 1,
            })).tournamentGroupId
      s.groups.set(name, groupId)
      for (let t = 0; t < perGroup; t++) {
        const tt = await s.addTeam(`T${n++}`)
        await s.organizer.patch(`${s.base}/tournament-teams/${tt}/group`, { groupId })
      }
    }
    await s.status("RegistrationClosed")
    return s
  }

  /** Puchar T1…Tn w parach T1-T2, T3-T4…, terminarz i start. */
  static async knockout(teamCount: number) {
    const s = await Scenario.create("Knockout")
    for (let i = 1; i <= teamCount; i++) await s.addTeam(`T${i}`)
    await s.status("RegistrationClosed")
    await s.organizer.post(`${s.base}/bracket/generate`, {
      categoryId: s.categoryId,
      roundOnePairings: Array.from({ length: teamCount / 2 }, (_, i) => ({
        homeSourceType: 1, homeGroupId: null, homePosition: null, homeTournamentTeamId: s.team(`T${2 * i + 1}`),
        awaySourceType: 1, awayGroupId: null, awayPosition: null, awayTournamentTeamId: s.team(`T${2 * i + 2}`),
      })),
    })
    return s
  }

  async start() {
    await this.generateSchedule()
    await this.status("InProgress")
  }

  schedule() {
    return this.organizer.get<Match[]>(`${this.base}/schedule`)
  }

  async match(a: string, b: string) {
    const [x, y] = [this.team(a), this.team(b)]
    return poll(`mecz ${a}-${b}`, async () =>
      (await this.schedule()).find(
        (m) =>
          (m.homeTournamentTeamId === x && m.awayTournamentTeamId === y) ||
          (m.homeTournamentTeamId === y && m.awayTournamentTeamId === x),
      ),
    )
  }

  /** Mecz rozegrany przez organizatora: zwycięzca wygrywa 1:0 (albo remis 1:1, gdy winner = null). */
  async play(a: string, b: string, winner: string | null, actor = this.organizer) {
    const m = await this.match(a, b)
    const path = `${this.base}/matches/${m.id}`
    await actor.post(`${path}/start`)
    const goal = (team: string) =>
      actor.post(`${path}/goals`, { tournamentTeamId: team, playerId: null, assistPlayerId: null, goalType: 5, minute: 10, second: null })
    if (winner) await goal(this.team(winner))
    else {
      await goal(this.team(a))
      await goal(this.team(b))
    }
    await actor.post(`${path}/finish`, winner
      ? { resultType: 2, winnerTournamentTeamId: this.team(winner) }
      : { resultType: 2, winnerTournamentTeamId: null })
    return m
  }

  /** Nowe konto dołączone kodem sędziego. */
  async referee() {
    const referee = await register("sedzia")
    const t = await this.organizer.get<{ refereeCode: string }>(this.base)
    await referee.post("/api/tournaments/join", { code: t.refereeCode })
    return referee
  }
}
