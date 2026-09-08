using Turniejo.Matches.Application.Abstractions;
using Turniejo.Matches.Application.DTOs;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Results;

namespace Turniejo.Matches.Application.Matches.GetMatchScore
{
    internal sealed class GetMatchScoreQueryHandler(IMatchesDbContext dbContext) : IQueryHandler<GetMatchScoreQuery, MatchScoreDto>
    {
        public async Task<Result<MatchScoreDto>> Handle(GetMatchScoreQuery request, CancellationToken cancellationToken)
        {
            var match = await dbContext.GetMatchByIdAsync(request.MatchId, cancellationToken);
            if (match == null || match.TournamentId != request.TournamentId)
                return Result.Failure<MatchScoreDto>(Error.NotFound("Match", request.MatchId));

            var dto = new MatchScoreDto(match.Id, match.HomeScore, match.AwayScore);

            return Result.Success(dto);
        }
    }
}
