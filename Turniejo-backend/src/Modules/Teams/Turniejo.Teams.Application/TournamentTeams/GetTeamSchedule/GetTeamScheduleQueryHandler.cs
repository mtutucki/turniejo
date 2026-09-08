using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Results;
using Turniejo.Teams.Application.DTOs;
using Turniejo.Teams.Application.Services;

namespace Turniejo.Teams.Application.TournamentTeams.GetTeamSchedule
{
    internal sealed class GetTeamScheduleQueryHandler(TeamMatchResolver matchResolver) : IQueryHandler<GetTeamScheduleQuery, IReadOnlyList<TeamMatchDto>>
    {
        public async Task<Result<IReadOnlyList<TeamMatchDto>>> Handle(GetTeamScheduleQuery request, CancellationToken cancellationToken)
        {
            var matches = await matchResolver.ResolveAsync(request.TournamentTeamId, m => m.Status != "Finished", cancellationToken);

            return Result.Success(matches);
        }
    }
}
