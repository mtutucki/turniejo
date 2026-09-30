import type { RoleAssignment } from "@/types/roles"

// Rodzaje kont wynikające z przypisanych ról — sterują nawigacją i stroną startową (/start).
// Jedno konto może mieć kilka (np. organizator, który jest też rodzicem).

/** Organizator — własna organizacja albo turniej, w którym ma rolę Organizer. */
export const isOrganizer = (roles: RoleAssignment[]) => roles.some((r) => r.role === "Organizer")

/** Ma organizację (baza drużyn i lista "Moje turnieje" są liczone w jej ramach). */
export const hasOrganization = (roles: RoleAssignment[]) =>
  roles.some((r) => r.role === "Organizer" && r.scopeType === "Organization")

export const isReferee = (roles: RoleAssignment[]) => roles.some((r) => r.role === "Referee")

/** Sztab przypisany do drużyny (panel trenera). */
export const isTeamStaff = (roles: RoleAssignment[]) =>
  roles.some(
    (r) =>
      r.scopeType === "TournamentTeam" && ["Coach", "AssistantCoach", "Manager"].includes(r.role),
  )

export const isParent = (roles: RoleAssignment[]) => roles.some((r) => r.role === "Parent")

/**
 * Turnieje, do których user dołączył kodem (sędzia, trener, kibic/rodzic) — bez własnych turniejów
 * organizatora, te są w "Moje turnieje". Jeden turniej może dać kilka ról (np. trener i sędzia).
 */
export function joinedTournaments(roles: RoleAssignment[]) {
  const byTournament = new Map<string, string[]>()
  for (const { role, scopeType, scopeId } of roles) {
    if (scopeType !== "Tournament" || !scopeId || role === "Organizer") continue
    byTournament.set(scopeId, [...(byTournament.get(scopeId) ?? []), role])
  }
  // Organizator turnieju, który dołączył też kodem, ma go już w swojej liście.
  for (const { role, scopeType, scopeId } of roles) {
    if (role === "Organizer" && scopeType === "Tournament" && scopeId) byTournament.delete(scopeId)
  }
  return [...byTournament].map(([id, tournamentRoles]) => ({ id, roles: tournamentRoles }))
}

export const hasJoinedTournament = (roles: RoleAssignment[]) => joinedTournaments(roles).length > 0

/**
 * Pracuje "w turnieju" (terminarz, tabela, drużyny aktywnego turnieju): organizator, sędzia albo
 * trener, który dołączył kodem. Sam rodzic/kibic tej sekcji nie potrzebuje — ma swój panel.
 */
export const worksInTournament = (roles: RoleAssignment[]) =>
  isOrganizer(roles) ||
  isReferee(roles) ||
  roles.some((r) => r.scopeType === "Tournament" && r.role === "Coach")

/**
 * Strona startowa po zalogowaniu wg najważniejszej roli. null = brak roli z własnym panelem
 * (świeże konto albo kibic czekający na przypisanie) — wtedy /start pokazuje ekran powitalny.
 */
export function landingPath(roles: RoleAssignment[]): string | null {
  if (isOrganizer(roles)) return "/tournaments"
  if (isReferee(roles)) return "/referee/matches"
  if (isTeamStaff(roles)) return "/coach/teams"
  if (isParent(roles)) return "/parent/players"
  return null
}
