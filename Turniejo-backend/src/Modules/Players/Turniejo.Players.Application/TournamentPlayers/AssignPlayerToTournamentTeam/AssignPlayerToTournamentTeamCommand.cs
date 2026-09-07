using Turniejo.Players.Application.Authorization;
using Turniejo.Players.Domain.Enums;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Enums;

namespace Turniejo.Players.Application.TournamentPlayers.AssignPlayerToTournamentTeam
{
    public sealed record AssignPlayerToTournamentTeamCommand(
        Guid TournamentId,
        Guid TournamentTeamId,
        Guid PlayerId,
        int? ShirtNumber,
        PlayerPosition? Position) : ICommand<AssignPlayerToTournamentTeamResponse>, IAuthorizedRequest
    {
        public string ScopeType => nameof(RoleScopeType.Tournament);
        public Guid ScopeId => TournamentId;
        public IReadOnlyCollection<string> AllowedRoles => PlayerRoles.OrganizerOnly;
    }
}
