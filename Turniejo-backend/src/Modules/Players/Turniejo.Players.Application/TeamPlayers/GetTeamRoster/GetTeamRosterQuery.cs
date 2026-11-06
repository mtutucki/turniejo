using Turniejo.Players.Application.DTOs;
using Turniejo.SharedKernel.CQRS;

namespace Turniejo.Players.Application.TeamPlayers.GetTeamRoster
{
    // Handler sprawdza TeamRosterAccessGuard (organizator organizacji drużyny albo sztab jej zgłoszeń).
    public sealed record GetTeamRosterQuery(Guid TeamId) : IQuery<TeamRosterDto>, IAuthenticatedOnlyRequest;
}
