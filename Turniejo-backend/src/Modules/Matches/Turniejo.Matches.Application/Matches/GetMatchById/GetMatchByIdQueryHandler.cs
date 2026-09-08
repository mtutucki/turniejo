using Turniejo.Matches.Application.Abstractions;
using Turniejo.Matches.Application.DTOs;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Results;

namespace Turniejo.Matches.Application.Matches.GetMatchById
{
    internal sealed class GetMatchByIdQueryHandler(IMatchesDbContext dbContext) : IQueryHandler<GetMatchByIdQuery, MatchDto>
    {
        public async Task<Result<MatchDto>> Handle(GetMatchByIdQuery request, CancellationToken cancellationToken)
        {
            var match = await dbContext.GetMatchByIdAsync(request.MatchId, cancellationToken);
            if (match == null || match.TournamentId != request.TournamentId)
                return Result.Failure<MatchDto>(Error.NotFound("Match", request.MatchId));

            var dto = new MatchDto(
                match.Id, match.TournamentId, match.CategoryId, match.GroupId, match.PitchId,
                match.HomeTournamentTeamId, match.AwayTournamentTeamId, match.RefereeId, match.ScheduledAt,
                match.Status.ToString(), match.StageType.ToString(), match.HomeScore, match.AwayScore, match.ResultType.ToString());

            return Result.Success(dto);
        }
    }
}
