using Turniejo.Players.Application.Authorization;
using Turniejo.Players.Domain.Enums;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Enums;

namespace Turniejo.Players.Application.TournamentPlayers.UpdateTournamentPlayer
{
    public sealed record UpdateTournamentPlayerCommand(
        Guid TournamentId,
        Guid TournamentTeamId,
        Guid TournamentPlayerId,
        int? ShirtNumber,
        PlayerPosition? Position,
        TournamentPlayerStatus Status) : ICommand<UpdateTournamentPlayerResponse>, IAuthorizedRequest
    {
        public string ScopeType => nameof(RoleScopeType.Tournament);
        public Guid ScopeId => TournamentId;
        public IReadOnlyCollection<string> AllowedRoles => PlayerRoles.OrganizerOnly;
    }
}
