using Turniejo.SharedKernel.CQRS;
using Turniejo.Teams.Application.DTOs;

namespace Turniejo.Teams.Application.TournamentTeams.GetMyTeams
{
    // Handler filtruje po TeamStaff.UserId == CurrentUserId — brak parametrów wejściowych.
    public sealed record GetMyTeamsQuery : IQuery<IReadOnlyList<MyTeamDto>>, IAuthenticatedOnlyRequest;
}
