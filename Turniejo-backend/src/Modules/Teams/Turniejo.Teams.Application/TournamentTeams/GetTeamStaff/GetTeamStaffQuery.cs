using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Enums;
using Turniejo.Teams.Application.Authorization;
using Turniejo.Teams.Application.DTOs;

namespace Turniejo.Teams.Application.TournamentTeams.GetTeamStaff
{
    public sealed record GetTeamStaffQuery(Guid TournamentId, Guid TournamentTeamId)
        : IQuery<IReadOnlyList<TeamStaffDto>>, IAuthorizedRequest, IAlternateAuthorizedScope
    {
        // Primary: organizator całego turnieju.
        public string ScopeType => nameof(RoleScopeType.Tournament);
        public Guid ScopeId => TournamentId;
        public IReadOnlyCollection<string> AllowedRoles => TeamRoles.OrganizerOnly;

        // Alternate: Policy Coach — staff przypisany do TEJ konkretnej drużyny turniejowej.
        public string AlternateScopeType => nameof(RoleScopeType.TournamentTeam);
        public Guid AlternateScopeId => TournamentTeamId;
        public IReadOnlyCollection<string> AlternateAllowedRoles => TeamRoles.AnyTeamStaff;
    }
}
