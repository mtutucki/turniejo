using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Results;
using Turniejo.Standings.Application.Abstractions;
using Turniejo.Standings.Application.DTOs;

namespace Turniejo.Standings.Application.Standings.GetStandings
{
    internal sealed class GetStandingsQueryHandler(IStandingsDbContext dbContext) : IQueryHandler<GetStandingsQuery, IReadOnlyList<StandingDto>>
    {
        public async Task<Result<IReadOnlyList<StandingDto>>> Handle(GetStandingsQuery request, CancellationToken cancellationToken)
        {
            var standings = await dbContext.GetStandingsByGroupIdAsync(request.GroupId, cancellationToken);

            var dtos = standings
                .OrderBy(s => s.Position)
                .Select(s => new StandingDto(
                    s.Id, s.TournamentId, s.CategoryId, s.GroupId, s.TournamentTeamId,
                    s.MatchesPlayed, s.Wins, s.Draws, s.Losses, s.GoalsFor, s.GoalsAgainst,
                    s.GoalDifference, s.Points, s.Position))
                .ToList();

            return Result.Success<IReadOnlyList<StandingDto>>(dtos);
        }
    }
}
