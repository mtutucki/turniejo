// 1:1 z Turniejo.SharedKernel.Enums.UserRole
export type UserRole =
  | "Organizer"
  | "Coach"
  | "AssistantCoach"
  | "Manager"
  | "Referee"
  | "Parent"
  | "Player"
  | "Admin"
  | "Viewer"

// 1:1 z Turniejo.SharedKernel.Enums.RoleScopeType
export type ResourceType = "Organization" | "Tournament" | "TournamentTeam" | "Player" | "Match"

/** Element odpowiedzi GET /api/me/roles. */
export type RoleAssignment = {
  role: UserRole
  scopeType: ResourceType
  scopeId: string | null
}
