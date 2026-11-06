using Turniejo.SharedKernel.CQRS;

namespace Turniejo.Players.Application.TeamPlayers.RestoreTeamPlayer
{
    // Były zawodnik wraca do kadry. Handler sprawdza TeamRosterAccessGuard.
    public sealed record RestoreTeamPlayerCommand(Guid TeamId, Guid PlayerId) : ICommand<RestoreTeamPlayerResponse>, IAuthenticatedOnlyRequest;

    public sealed record RestoreTeamPlayerResponse(Guid PlayerId, int? ShirtNumber);
}
