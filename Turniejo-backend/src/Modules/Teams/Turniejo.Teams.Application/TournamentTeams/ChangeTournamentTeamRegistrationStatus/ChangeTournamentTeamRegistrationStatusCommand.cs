using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Enums;
using Turniejo.Teams.Application.Authorization;
using Turniejo.Teams.Domain.Enums;

namespace Turniejo.Teams.Application.TournamentTeams.ChangeTournamentTeamRegistrationStatus
{
    public sealed record ChangeTournamentTeamRegistrationStatusCommand(
        Guid TournamentId,
        Guid TournamentTeamId,
        RegistrationStatus NewStatus) : ICommand<ChangeTournamentTeamRegistrationStatusResponse>, IAuthorizedRequest
    {
        public string ScopeType => nameof(RoleScopeType.Tournament);
        public Guid ScopeId => TournamentId;
        public IReadOnlyCollection<string> AllowedRoles => TeamRoles.OrganizerOnly;
    }
}
