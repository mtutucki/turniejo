using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Results;
using Turniejo.Standings.Application.Abstractions;
using Turniejo.Standings.Application.DTOs;

namespace Turniejo.Standings.Application.Standings.GetStandings
{
    internal sealed class GetStandingsQueryHandler(
        IStandingsDbContext dbContext,
        ITournamentModule tournamentModule,
        ITeamModule teamModule,
        IMatchModule matchModule,
        IBracketModule bracketModule) : IQueryHandler<GetStandingsQuery, IReadOnlyList<StandingDto>>
    {
        public async Task<Result<IReadOnlyList<StandingDto>>> Handle(GetStandingsQuery request, CancellationToken cancellationToken)
        {
            var standings = await dbContext.GetStandingsByGroupIdAsync(request.GroupId, cancellationToken);

            // GetStandingsByGroupIdAsync filtruje tylko po GroupId — bez tego organizator autoryzowany
            // na TournamentId z route'a mógłby podać GroupId należący do INNEGO turnieju i dostać jego
            // tabelę (autoryzacja sprawdza tylko rolę na request.TournamentId, nie że GroupId do niego należy).
            var dtos = await GroupTable.ReadAsync(
                request.TournamentId, request.GroupId, standings, tournamentModule, teamModule, matchModule, bracketModule, cancellationToken);

            return Result.Success<IReadOnlyList<StandingDto>>(dtos);
        }
    }
}
