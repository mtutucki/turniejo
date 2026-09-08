using Turniejo.Matches.Application.Abstractions;
using Turniejo.Matches.Application.DTOs;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Results;

namespace Turniejo.Matches.Application.Matches.GetAssignedMatches
{
    internal sealed class GetAssignedMatchesQueryHandler(
        IMatchesDbContext dbContext,
        ICurrentUserContext currentUserContext) : IQueryHandler<GetAssignedMatchesQuery, IReadOnlyList<MatchDto>>
    {
        public async Task<Result<IReadOnlyList<MatchDto>>> Handle(GetAssignedMatchesQuery request, CancellationToken cancellationToken)
        {
            var matches = await dbContext.GetMatchesByRefereeIdAsync(currentUserContext.UserId, cancellationToken);

            var dtos = matches
                .Select(m => new MatchDto(
                    m.Id, m.TournamentId, m.CategoryId, m.GroupId, m.PitchId,
                    m.HomeTournamentTeamId, m.AwayTournamentTeamId, m.RefereeId, m.ScheduledAt,
                    m.Status.ToString(), m.StageType.ToString(), m.HomeScore, m.AwayScore, m.ResultType.ToString()))
                .ToList();

            return Result.Success<IReadOnlyList<MatchDto>>(dtos);
        }
    }
}
