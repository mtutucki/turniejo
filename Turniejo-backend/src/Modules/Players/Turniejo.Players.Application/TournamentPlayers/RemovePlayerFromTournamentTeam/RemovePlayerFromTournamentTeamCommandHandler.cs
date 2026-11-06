using Turniejo.Players.Application.Abstractions;
using Turniejo.Players.Application.Services;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Results;

namespace Turniejo.Players.Application.TournamentPlayers.RemovePlayerFromTournamentTeam
{
    internal sealed class RemovePlayerFromTournamentTeamCommandHandler(
        IPlayersDbContext dbContext,
        ITeamModule teamModule,
        SquadRemoval squadRemoval,
        SquadEditGuard squadEditGuard,
        ICurrentUserContext currentUserContext) : ICommandHandler<RemovePlayerFromTournamentTeamCommand, RemovePlayerFromTournamentTeamResponse>
    {
        public async Task<Result<RemovePlayerFromTournamentTeamResponse>> Handle(RemovePlayerFromTournamentTeamCommand request, CancellationToken cancellationToken)
        {
            var tournamentPlayer = await dbContext.GetTournamentPlayerByIdAsync(request.TournamentPlayerId, cancellationToken);
            if (tournamentPlayer == null || tournamentPlayer.TournamentTeamId != request.TournamentTeamId)
                return Result.Failure<RemovePlayerFromTournamentTeamResponse>(Error.NotFound("TournamentPlayer", request.TournamentPlayerId));

            // Jak przy edycji: para turniej–drużyna musi się zgadzać, bo pipeline sprawdza tylko jeden z zakresów.
            var tournamentTeam = await teamModule.GetTournamentTeamAsync(tournamentPlayer.TournamentTeamId, cancellationToken);
            if (tournamentTeam == null || tournamentTeam.TournamentId != request.TournamentId)
                return Result.Failure<RemovePlayerFromTournamentTeamResponse>(Error.NotFound("TournamentTeam", request.TournamentTeamId));

            await squadEditGuard.EnsureCanEditSquadAsync(tournamentTeam.TournamentId, currentUserContext.UserId, cancellationToken);

            var removal = await squadRemoval.RemoveAsync(tournamentPlayer, cancellationToken);
            if (removal.IsFailure)
                return Result.Failure<RemovePlayerFromTournamentTeamResponse>(removal.Error);

            var deleted = removal.Value;
            if (deleted)
            {
                // Profil dodany pomyłkowo nie zostaje w bazie: kasujemy go, jeśli nie gra w innym składzie,
                // nie jest w żadnej kadrze i nie ma powiązanego konta ani opiekunów. Zawodnik z kadry zostaje
                // w kadrze: usunięcie ze składu turnieju to nie usunięcie z drużyny.
                var player = await dbContext.GetPlayerByIdAsync(tournamentPlayer.PlayerId, cancellationToken);
                if (player != null && player.UserId == null)
                {
                    var otherSquads = await dbContext.GetTournamentPlayersByPlayerIdAsync(player.Id, cancellationToken);
                    var guardians = await dbContext.GetPlayerGuardiansByPlayerIdAsync(player.Id, cancellationToken);
                    var rosters = await dbContext.GetTeamPlayersByPlayerIdAsync(player.Id, cancellationToken);
                    if (otherSquads.All(tp => tp.Id == tournamentPlayer.Id) && guardians.Count == 0 && rosters.Count == 0)
                        await dbContext.RemovePlayerAsync(player, cancellationToken);
                }
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            return Result.Success(new RemovePlayerFromTournamentTeamResponse(tournamentPlayer.Id, deleted));
        }
    }
}
