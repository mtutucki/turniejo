using Turniejo.Players.Application.DTOs;
using Turniejo.SharedKernel.CQRS;

namespace Turniejo.Players.Application.TeamPlayers.GetTeamHistory
{
    /// <summary>Statystyki drużyny z bazy we wszystkich turniejach. Dostęp jak do kadry (TeamRosterAccessGuard).</summary>
    public sealed record GetTeamHistoryQuery(Guid TeamId) : IQuery<TeamHistoryDto>, IAuthenticatedOnlyRequest;
}
