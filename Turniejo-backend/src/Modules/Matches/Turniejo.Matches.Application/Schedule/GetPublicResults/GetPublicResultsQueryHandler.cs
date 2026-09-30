using Turniejo.Matches.Application.Abstractions;
using Turniejo.Matches.Application.DTOs;
using Turniejo.Matches.Domain.Enums;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Results;

namespace Turniejo.Matches.Application.Schedule.GetPublicResults
{
    internal sealed class GetPublicResultsQueryHandler(
        IMatchesDbContext dbContext,
        ITournamentModule tournamentModule) : IQueryHandler<GetPublicResultsQuery, IReadOnlyList<MatchDto>>
    {
        public async Task<Result<IReadOnlyList<MatchDto>>> Handle(GetPublicResultsQuery request, CancellationToken cancellationToken)
        {
            var tournament = await tournamentModule.GetPublicTournamentBySlugAsync(request.Slug, cancellationToken);
            if (tournament == null)
                return Result.Failure<IReadOnlyList<MatchDto>>(Error.NotFound("Tournament", request.Slug));

            if (!tournament.Settings.ShowSchedule)
                return Result.Success<IReadOnlyList<MatchDto>>([]);

            var matches = await dbContext.GetMatchesByTournamentIdAsync(tournament.Id, cancellationToken);

            var dtos = matches
                .Where(m => m.Status == MatchStatus.Finished)
                .OrderByDescending(m => m.FinishedAt)
                // RefereeId celowo null — kto sędziuje to wewnętrzna informacja organizacyjna, nie
                // coś do publicznego ujawnienia przez stronę turnieju.
                .Select(m => new MatchDto(
                    m.Id, m.TournamentId, m.CategoryId, m.GroupId, m.PitchId,
                    m.HomeTournamentTeamId, m.AwayTournamentTeamId, RefereeId: null, m.ScheduledAt,
                    m.Status.ToString(), m.StageType.ToString(), m.HomeScore, m.AwayScore, m.ResultType.ToString(),
                    m.HomePenaltyScore, m.AwayPenaltyScore))
                .ToList();

            return Result.Success<IReadOnlyList<MatchDto>>(dtos);
        }
    }
}
