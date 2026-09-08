using Turniejo.Matches.Application.Abstractions;
using Turniejo.Matches.Application.DTOs;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Results;

namespace Turniejo.Matches.Application.Matches.GetMatchEvents
{
    internal sealed class GetMatchEventsQueryHandler(IMatchesDbContext dbContext) : IQueryHandler<GetMatchEventsQuery, IReadOnlyList<MatchEventDto>>
    {
        public async Task<Result<IReadOnlyList<MatchEventDto>>> Handle(GetMatchEventsQuery request, CancellationToken cancellationToken)
        {
            var match = await dbContext.GetMatchByIdAsync(request.MatchId, cancellationToken);
            if (match == null || match.TournamentId != request.TournamentId)
                return Result.Failure<IReadOnlyList<MatchEventDto>>(Error.NotFound("Match", request.MatchId));

            var events = await dbContext.GetMatchEventsByMatchIdAsync(match.Id, cancellationToken);

            var dtos = events
                .Select(e => new MatchEventDto(
                    e.Id, e.MatchId, e.Type.ToString(), e.TournamentTeamId, e.PlayerId, e.RelatedPlayerId,
                    e.Minute, e.Second, e.CreatedByUserId, e.CreatedAt))
                .ToList();

            return Result.Success<IReadOnlyList<MatchEventDto>>(dtos);
        }
    }
}
