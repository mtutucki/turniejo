using Turniejo.Matches.Application.Abstractions;
using Turniejo.Matches.Application.DTOs;
using Turniejo.Matches.Application.Services;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Exceptions;
using Turniejo.SharedKernel.Results;

namespace Turniejo.Matches.Application.Matches.GetMatchLocation
{
    internal sealed class GetMatchLocationQueryHandler(
        IMatchesDbContext dbContext,
        MatchAccessGuard accessGuard,
        ICurrentUserContext currentUserContext) : IQueryHandler<GetMatchLocationQuery, MatchLocationDto>
    {
        public async Task<Result<MatchLocationDto>> Handle(GetMatchLocationQuery request, CancellationToken cancellationToken)
        {
            var match = await dbContext.GetMatchByIdAsync(request.MatchId, cancellationToken);
            if (match == null)
                return Result.Failure<MatchLocationDto>(Error.NotFound("Match", request.MatchId));

            var canView = await accessGuard.CanViewAsync(match, currentUserContext.UserId, cancellationToken);
            if (!canView)
                throw new UnauthorizedException();

            var pitch = match.PitchId.HasValue
                ? await dbContext.GetPitchByIdAsync(match.PitchId.Value, cancellationToken)
                : null;

            var dto = new MatchLocationDto(match.Id, match.ScheduledAt, match.PitchId, pitch?.Name, pitch?.Description);

            return Result.Success(dto);
        }
    }
}
