using Turniejo.Matches.Application.Abstractions;
using Turniejo.Matches.Domain.Enums;
using Turniejo.SharedKernel.Results;

namespace Turniejo.Matches.Application.Services
{
    // Czerwona kartka wyklucza zawodnika z dalszej gry w meczu: nie może już dostać kartki, strzelić gola
    // ani zaliczyć asysty. Druga żółta w tym samym meczu to czerwona. Liczą się tylko aktywne
    // (niecofnięte) zdarzenia — cofnięcie czerwonej przywraca zawodnika do gry.
    public sealed class SendOffRules(IMatchesDbContext dbContext)
    {
        public async Task<Error?> EnsureNotSentOffAsync(Guid matchId, Guid? playerId, string field, CancellationToken cancellationToken)
        {
            if (!playerId.HasValue)
                return null;

            var redCards = await dbContext.GetActiveMatchEventsByTypeAsync(matchId, MatchEventType.RedCard, cancellationToken);
            return redCards.Any(e => e.PlayerId == playerId)
                ? Error.Validation(field, "Player has been sent off")
                : null;
        }

        /// <summary>Żółta dla zawodnika, który ma już żółtą w tym meczu, zapisuje się jako czerwona.</summary>
        public async Task<MatchEventType> ResolveCardTypeAsync(Guid matchId, Guid playerId, MatchEventType requested, CancellationToken cancellationToken)
        {
            if (requested != MatchEventType.YellowCard)
                return requested;

            var yellowCards = await dbContext.GetActiveMatchEventsByTypeAsync(matchId, MatchEventType.YellowCard, cancellationToken);
            return yellowCards.Any(e => e.PlayerId == playerId) ? MatchEventType.RedCard : requested;
        }
    }
}
