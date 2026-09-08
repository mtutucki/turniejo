using Turniejo.Matches.Application.Abstractions;
using Turniejo.Matches.Application.DTOs;
using Turniejo.Matches.Domain.Enums;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Results;

namespace Turniejo.Matches.Application.Matches.GetMatchClock
{
    internal sealed class GetMatchClockQueryHandler(IMatchesDbContext dbContext) : IQueryHandler<GetMatchClockQuery, MatchClockDto>
    {
        public async Task<Result<MatchClockDto>> Handle(GetMatchClockQuery request, CancellationToken cancellationToken)
        {
            var match = await dbContext.GetMatchByIdAsync(request.MatchId, cancellationToken);
            if (match == null || match.TournamentId != request.TournamentId)
                return Result.Failure<MatchClockDto>(Error.NotFound("Match", request.MatchId));

            var elapsedSeconds = 0;

            if (match.StartedAt.HasValue)
            {
                var referenceTime = match.Status switch
                {
                    MatchStatus.Live => DateTimeOffset.UtcNow,
                    MatchStatus.Paused => match.PausedAt ?? DateTimeOffset.UtcNow,
                    MatchStatus.Finished => match.FinishedAt ?? DateTimeOffset.UtcNow,
                    _ => match.StartedAt.Value
                };

                elapsedSeconds = Math.Max(0, (int)(referenceTime - match.StartedAt.Value).TotalSeconds - match.TotalPausedSeconds);
            }

            var dto = new MatchClockDto(match.Id, match.Status.ToString(), elapsedSeconds);

            return Result.Success(dto);
        }
    }
}
