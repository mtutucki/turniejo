using System.Text.Json;
using Turniejo.Players.Application.Abstractions;
using Turniejo.Players.Domain.Entities;
using Turniejo.Players.Domain.Enums;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.Outbox;
using Turniejo.SharedKernel.Results;

namespace Turniejo.Players.Application.Services
{
    // Usunięcie ze składu turniejowego. Zawodnik bez goli i kartek znika ze składu całkiem. Z wydarzeniami
    // w meczach dostaje status Removed, żeby statystyki i przebieg meczów dalej wskazywały na niego.
    // Zapis przy SaveChanges wywołującego.
    public sealed class SquadRemoval(IPlayersDbContext dbContext, IMatchModule matchModule)
    {
        /// <returns>true, gdy wpis został usunięty; false, gdy został ze statusem Removed.</returns>
        public async Task<Result<bool>> RemoveAsync(TournamentPlayer tournamentPlayer, CancellationToken cancellationToken = default)
        {
            var events = await matchModule.GetMatchEventsByTournamentTeamIdAsync(tournamentPlayer.TournamentTeamId, cancellationToken);
            var hasEvents = events.Any(e => e.PlayerId == tournamentPlayer.PlayerId || e.RelatedPlayerId == tournamentPlayer.PlayerId);

            if (!hasEvents)
            {
                await dbContext.RemoveTournamentPlayerAsync(tournamentPlayer, cancellationToken);
                return Result.Success(true);
            }

            // Numer zwalniamy (unikalny w drużynie), żeby dało się go dać komuś innemu.
            var result = tournamentPlayer.UpdateDetails(null, tournamentPlayer.Position, TournamentPlayerStatus.Removed);
            if (result.IsFailure)
                return Result.Failure<bool>(result.Error);

            foreach (var domainEvent in tournamentPlayer.DomainEvents)
            {
                await dbContext.AddOutboxMessageAsync(OutboxMessage.Create(
                    domainEvent.GetType().AssemblyQualifiedName!,
                    JsonSerializer.Serialize(domainEvent, domainEvent.GetType())), cancellationToken);
            }

            return Result.Success(false);
        }
    }
}
