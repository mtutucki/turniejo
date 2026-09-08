using Turniejo.Players.Application.Abstractions;
using Turniejo.Players.Application.DTOs;
using Turniejo.Players.Application.Services;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Results;

namespace Turniejo.Players.Application.PlayerGuardians.GetMyChildStanding
{
    internal sealed class GetMyChildStandingQueryHandler(
        IPlayersDbContext dbContext,
        GuardianAccessChecker accessChecker,
        ITeamModule teamModule,
        IStandingModule standingModule,
        ICurrentUserContext currentUserContext) : IQueryHandler<GetMyChildStandingQuery, IReadOnlyList<MyChildStandingDto>>
    {
        public async Task<Result<IReadOnlyList<MyChildStandingDto>>> Handle(GetMyChildStandingQuery request, CancellationToken cancellationToken)
        {
            await accessChecker.EnsureGuardianAsync(request.PlayerId, currentUserContext.UserId, cancellationToken);

            var tournamentPlayers = await dbContext.GetTournamentPlayersByPlayerIdAsync(request.PlayerId, cancellationToken);
            var tournamentTeamIds = tournamentPlayers.Select(p => p.TournamentTeamId).Distinct().ToList();

            var dtos = new List<MyChildStandingDto>();
            foreach (var tournamentTeamId in tournamentTeamIds)
            {
                var tournamentTeam = await teamModule.GetTournamentTeamAsync(tournamentTeamId, cancellationToken);
                if (tournamentTeam?.GroupId == null)
                    continue;

                var standings = await standingModule.GetGroupStandingsAsync(tournamentTeam.GroupId.Value, cancellationToken);
                dtos.Add(new MyChildStandingDto(tournamentTeamId, tournamentTeam.GroupId.Value, standings));
            }

            return Result.Success<IReadOnlyList<MyChildStandingDto>>(dtos);
        }
    }
}
