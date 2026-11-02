using System.Text.Json;
using Turniejo.Players.Application.Abstractions;
using Turniejo.Players.Application.Services;
using Turniejo.Players.Domain.Enums;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Outbox;
using Turniejo.SharedKernel.Results;

namespace Turniejo.Players.Application.TournamentPlayers.RemovePlayerFromTournamentTeam
{
    internal sealed class RemovePlayerFromTournamentTeamCommandHandler(
        IPlayersDbContext dbContext,
        ITeamModule teamModule,
        IMatchModule matchModule,
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

            var events = await matchModule.GetMatchEventsByTournamentTeamIdAsync(tournamentTeam.Id, cancellationToken);
            var hasEvents = events.Any(e => e.PlayerId == tournamentPlayer.PlayerId || e.RelatedPlayerId == tournamentPlayer.PlayerId);

            if (hasEvents)
            {
                // Gole i kartki zostają przy zawodniku. Numer zwalniamy (unikalny w drużynie), żeby dało się go dać komuś innemu.
                var result = tournamentPlayer.UpdateDetails(null, tournamentPlayer.Position, TournamentPlayerStatus.Removed);
                if (result.IsFailure)
                    return Result.Failure<RemovePlayerFromTournamentTeamResponse>(result.Error);

                foreach (var domainEvent in tournamentPlayer.DomainEvents)
                {
                    await dbContext.AddOutboxMessageAsync(OutboxMessage.Create(
                        domainEvent.GetType().AssemblyQualifiedName!,
                        JsonSerializer.Serialize(domainEvent, domainEvent.GetType())), cancellationToken);
                }
            }
            else
            {
                await dbContext.RemoveTournamentPlayerAsync(tournamentPlayer, cancellationToken);

                // Profil dodany pomyłkowo nie zostaje w bazie: kasujemy go, jeśli nie gra w innym składzie
                // i nie ma powiązanego konta ani opiekunów.
                var player = await dbContext.GetPlayerByIdAsync(tournamentPlayer.PlayerId, cancellationToken);
                if (player != null && player.UserId == null)
                {
                    var otherSquads = await dbContext.GetTournamentPlayersByPlayerIdAsync(player.Id, cancellationToken);
                    var guardians = await dbContext.GetPlayerGuardiansByPlayerIdAsync(player.Id, cancellationToken);
                    if (otherSquads.All(tp => tp.Id == tournamentPlayer.Id) && guardians.Count == 0)
                        await dbContext.RemovePlayerAsync(player, cancellationToken);
                }
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            return Result.Success(new RemovePlayerFromTournamentTeamResponse(tournamentPlayer.Id, !hasEvents));
        }
    }
}
