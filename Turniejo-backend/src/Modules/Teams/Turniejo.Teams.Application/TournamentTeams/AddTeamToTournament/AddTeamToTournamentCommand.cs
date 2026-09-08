using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Enums;
using Turniejo.Teams.Application.Authorization;

namespace Turniejo.Teams.Application.TournamentTeams.AddTeamToTournament
{
    public sealed record AddTeamToTournamentCommand(
        Guid TournamentId,
        Guid TeamId,
        Guid CategoryId) : ICommand<AddTeamToTournamentResponse>, IAuthorizedRequest
    {
        public string ScopeType => nameof(RoleScopeType.Tournament);
        public Guid ScopeId => TournamentId;
        public IReadOnlyCollection<string> AllowedRoles => TeamRoles.OrganizerOnly;
    }
}
