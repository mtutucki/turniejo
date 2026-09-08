using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Results;
using Turniejo.Standings.Application.Abstractions;
using Turniejo.Standings.Application.DTOs;

namespace Turniejo.Standings.Application.Standings.GetPublicStandings
{
    internal sealed class GetPublicStandingsQueryHandler(
        IStandingsDbContext dbContext,
        ITournamentModule tournamentModule) : IQueryHandler<GetPublicStandingsQuery, IReadOnlyList<StandingDto>>
    {
        public async Task<Result<IReadOnlyList<StandingDto>>> Handle(GetPublicStandingsQuery request, CancellationToken cancellationToken)
        {
            var tournament = await tournamentModule.GetPublicTournamentBySlugAsync(request.Slug, cancellationToken);
            if (tournament == null)
                return Result.Failure<IReadOnlyList<StandingDto>>(Error.NotFound("Tournament", request.Slug));

            if (!tournament.Settings.ShowStandings)
                return Result.Success<IReadOnlyList<StandingDto>>([]);

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
