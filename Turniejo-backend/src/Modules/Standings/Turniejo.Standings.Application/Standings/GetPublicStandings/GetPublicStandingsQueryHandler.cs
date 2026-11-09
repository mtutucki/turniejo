using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Results;
using Turniejo.Standings.Application.Abstractions;
using Turniejo.Standings.Application.DTOs;

namespace Turniejo.Standings.Application.Standings.GetPublicStandings
{
    internal sealed class GetPublicStandingsQueryHandler(
        IStandingsDbContext dbContext,
        ITournamentModule tournamentModule,
        ITeamModule teamModule,
        IMatchModule matchModule) : IQueryHandler<GetPublicStandingsQuery, IReadOnlyList<StandingDto>>
    {
        public async Task<Result<IReadOnlyList<StandingDto>>> Handle(GetPublicStandingsQuery request, CancellationToken cancellationToken)
        {
            var tournament = await tournamentModule.GetPublicTournamentBySlugAsync(request.Slug, cancellationToken);
            if (tournament == null)
                return Result.Failure<IReadOnlyList<StandingDto>>(Error.NotFound("Tournament", request.Slug));

            if (!tournament.Settings.ShowStandings)
                return Result.Success<IReadOnlyList<StandingDto>>([]);

            var standings = await dbContext.GetStandingsByGroupIdAsync(request.GroupId, cancellationToken);

            // GetStandingsByGroupIdAsync filtruje tylko po GroupId — bez tego ktoś podający slug
            // dowolnego publicznego turnieju razem z GroupId z INNEGO turnieju (nawet prywatnego)
            // dostałby jego tabelę. GroupId nie jest sekretem (widoczny w publicznym JSON-ie grup).
            var dtos = await GroupTable.ReadAsync(
                tournament.Id, request.GroupId, standings, tournamentModule, teamModule, matchModule, cancellationToken);

            return Result.Success<IReadOnlyList<StandingDto>>(dtos);
        }
    }
}
