using Turniejo.Players.Application.Authorization;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.Enums;

namespace Turniejo.Players.Application.Services
{
    public enum TeamRosterAccess
    {
        None,
        View,
        Edit
    }

    // Kadra należy do drużyny z bazy (Team), a role sztabu są nadawane per zgłoszenie (TournamentTeam).
    // Dlatego: organizator organizacji drużyny edytuje, trener dowolnego zgłoszenia tej drużyny też,
    // a asystent i kierownik tylko oglądają. Brak dopasowania = brak dostępu (fail-closed).
    public sealed class TeamRosterAccessGuard(
        ITeamModule teamModule,
        IAuthorizationService authorizationService)
    {
        public async Task<TeamRosterAccess> GetAccessAsync(Guid teamId, Guid userId, CancellationToken cancellationToken = default)
        {
            var organizationId = await teamModule.GetTeamOrganizationIdAsync(teamId, cancellationToken);
            if (organizationId == null)
                return TeamRosterAccess.None;

            var isOrganizer = await authorizationService.HasAnyRoleAsync(
                userId, nameof(RoleScopeType.Organization), organizationId.Value, PlayerRoles.OrganizerOnly, cancellationToken);
            if (isOrganizer)
                return TeamRosterAccess.Edit;

            var access = TeamRosterAccess.None;
            var registrations = await teamModule.GetTournamentTeamsByTeamIdAsync(teamId, cancellationToken);
            foreach (var registration in registrations)
            {
                var isCoach = await authorizationService.HasAnyRoleAsync(
                    userId, nameof(RoleScopeType.TournamentTeam), registration.Id, PlayerRoles.CoachOnly, cancellationToken);
                if (isCoach)
                    return TeamRosterAccess.Edit;

                if (access == TeamRosterAccess.None)
                {
                    var isStaff = await authorizationService.HasAnyRoleAsync(
                        userId, nameof(RoleScopeType.TournamentTeam), registration.Id, PlayerRoles.AnyTeamStaff, cancellationToken);
                    if (isStaff)
                        access = TeamRosterAccess.View;
                }
            }

            return access;
        }
    }
}
