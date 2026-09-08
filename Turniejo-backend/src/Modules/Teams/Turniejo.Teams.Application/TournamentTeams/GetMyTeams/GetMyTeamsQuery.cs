using Turniejo.SharedKernel.CQRS;
using Turniejo.Teams.Application.DTOs;

namespace Turniejo.Teams.Application.TournamentTeams.GetMyTeams
{
    public sealed record GetMyTeamsQuery : IQuery<IReadOnlyList<MyTeamDto>>;
}
