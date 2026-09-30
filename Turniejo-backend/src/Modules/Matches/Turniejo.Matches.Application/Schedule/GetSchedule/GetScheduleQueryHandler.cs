using Turniejo.Matches.Application.Abstractions;
using Turniejo.Matches.Application.DTOs;
using Turniejo.Matches.Application.Services;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Results;

namespace Turniejo.Matches.Application.Schedule.GetSchedule
{
    internal sealed class GetScheduleQueryHandler(IMatchesDbContext dbContext, RefereeIdVisibility refereeIdVisibility) : IQueryHandler<GetScheduleQuery, IReadOnlyList<MatchDto>>
    {
        public async Task<Result<IReadOnlyList<MatchDto>>> Handle(GetScheduleQuery request, CancellationToken cancellationToken)
        {
            var matches = await dbContext.GetMatchesByTournamentIdAsync(request.TournamentId, cancellationToken);

            var visibleRefereeId = await refereeIdVisibility.GetFilterAsync(request.TournamentId, cancellationToken);
            var dtos = matches
                .OrderBy(m => m.ScheduledAt)
                .Select(m => new MatchDto(
                    m.Id, m.TournamentId, m.CategoryId, m.GroupId, m.PitchId,
                    m.HomeTournamentTeamId, m.AwayTournamentTeamId, visibleRefereeId(m.RefereeId), m.ScheduledAt,
                    m.Status.ToString(), m.StageType.ToString(), m.HomeScore, m.AwayScore, m.ResultType.ToString(),
                    m.HomePenaltyScore, m.AwayPenaltyScore))
                .ToList();

            return Result.Success<IReadOnlyList<MatchDto>>(dtos);
        }
    }
}
