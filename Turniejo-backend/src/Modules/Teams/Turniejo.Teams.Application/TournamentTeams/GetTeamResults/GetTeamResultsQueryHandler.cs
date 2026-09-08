using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Results;
using Turniejo.Teams.Application.DTOs;
using Turniejo.Teams.Application.Services;

namespace Turniejo.Teams.Application.TournamentTeams.GetTeamResults
{
    internal sealed class GetTeamResultsQueryHandler(TeamMatchResolver matchResolver) : IQueryHandler<GetTeamResultsQuery, IReadOnlyList<TeamMatchDto>>
    {
        public async Task<Result<IReadOnlyList<TeamMatchDto>>> Handle(GetTeamResultsQuery request, CancellationToken cancellationToken)
        {
            var matches = await matchResolver.ResolveAsync(request.TournamentTeamId, m => m.Status == "Finished", cancellationToken);

            return Result.Success(matches);
        }
    }
}
