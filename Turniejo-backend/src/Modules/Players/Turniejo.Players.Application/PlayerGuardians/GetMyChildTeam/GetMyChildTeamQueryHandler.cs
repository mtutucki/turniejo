using Turniejo.Players.Application.Abstractions;
using Turniejo.Players.Application.DTOs;
using Turniejo.Players.Application.Services;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Results;

namespace Turniejo.Players.Application.PlayerGuardians.GetMyChildTeam
{
    internal sealed class GetMyChildTeamQueryHandler(
        IPlayersDbContext dbContext,
        GuardianAccessChecker accessChecker,
        ITeamModule teamModule,
        ITournamentModule tournamentModule,
        ICurrentUserContext currentUserContext) : IQueryHandler<GetMyChildTeamQuery, IReadOnlyList<MyChildTeamDto>>
    {
        public async Task<Result<IReadOnlyList<MyChildTeamDto>>> Handle(GetMyChildTeamQuery request, CancellationToken cancellationToken)
        {
            await accessChecker.EnsureGuardianAsync(request.PlayerId, currentUserContext.UserId, cancellationToken);

            var tournamentPlayers = await dbContext.GetTournamentPlayersByPlayerIdAsync(request.PlayerId, cancellationToken);

            var dtos = new List<MyChildTeamDto>();
            foreach (var tournamentPlayer in tournamentPlayers)
            {
                var tournamentTeam = await teamModule.GetTournamentTeamAsync(tournamentPlayer.TournamentTeamId, cancellationToken);
                if (tournamentTeam == null)
                    continue;

                // Odrzucona/wycofana drużyna nie gra w turnieju — rodzic nie widzi jej wśród drużyn dziecka.
                if (tournamentTeam.RegistrationStatus is "Rejected" or "Withdrawn")
                    continue;

                var team = await teamModule.GetTeamAsync(tournamentTeam.TeamId, cancellationToken);
                var tournament = await tournamentModule.GetTournamentAsync(tournamentTeam.TournamentId, cancellationToken);

                dtos.Add(new MyChildTeamDto(
                    tournamentPlayer.Id, tournamentTeam.Id, tournamentTeam.TournamentId, tournament?.Name ?? "Unknown",
                    tournamentTeam.TeamId, team?.Name ?? "Unknown", tournamentTeam.GroupId,
                    tournamentPlayer.ShirtNumber, tournamentPlayer.Position?.ToString()));
            }

            return Result.Success<IReadOnlyList<MyChildTeamDto>>(dtos);
        }
    }
}
