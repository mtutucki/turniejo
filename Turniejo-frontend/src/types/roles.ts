// 1:1 z Turniejo.SharedKernel.Enums.UserRole — backend serializuje nazwy, nie liczby.
export const UserRole = {
  Organizer: "Organizer",
  Coach: "Coach",
  AssistantCoach: "AssistantCoach",
  Manager: "Manager",
  Referee: "Referee",
  Parent: "Parent",
  Player: "Player",
  Admin: "Admin",
  Viewer: "Viewer",
} as const
export type UserRole = (typeof UserRole)[keyof typeof UserRole]

// 1:1 z Turniejo.SharedKernel.Enums.RoleScopeType
export type ResourceType = "Organization" | "Tournament" | "TournamentTeam" | "Player" | "Match"

/** Element odpowiedzi GET /api/me/roles. */
export type RoleAssignment = {
  role: UserRole
  scopeType: ResourceType
  scopeId: string | null
}
