using System.Text.Json;
using MediatR;
using Turniejo.Matches.Application.Abstractions;
using Turniejo.SharedKernel.Outbox;
using Turniejo.Tournaments.Domain.Enums;
using Turniejo.Tournaments.Domain.Events;

namespace Turniejo.Matches.Application.EventHandlers
{
    // Anulowanie turnieju nie zatrzymywało jego meczów — StartMatchCommandHandler sprawdzał tylko
    // Match.Status, nigdy status turnieju, więc sędzia mógł dalej prowadzić mecz w anulowanym
    // turnieju. Zamiast dokładać cross-modułowy lookup do każdej komendy mutującej mecz (Start,
    // Pause, Finish, AddGoal...), Match.Status zostaje jedynym źródłem prawdy: anulowanie kaskaduje
    // na jeszcze nierozegrane mecze, więc Start()/Finish() same odmówią z Conflict.Status.
    // To samo przy zakończeniu turnieju: nierozpoczęte mecze dało się dalej startować w zakończonym
    // turnieju (trwające blokują samo zakończenie — ChangeTournamentStatusCommandHandler).
    internal sealed class TournamentStatusChangedHandler(IMatchesDbContext dbContext) : INotificationHandler<TournamentStatusChanged>
    {
        public async Task Handle(TournamentStatusChanged notification, CancellationToken cancellationToken)
        {
            var reason = notification.NewStatus switch
            {
                TournamentStatus.Cancelled => "Turniej został anulowany.",
                TournamentStatus.Finished => "Turniej został zakończony.",
                _ => null
            };
            if (reason is null)
                return;

            var matches = await dbContext.GetCancellableMatchesByTournamentIdAsync(notification.TournamentId, cancellationToken);
            if (matches.Count == 0)
                return;

            foreach (var match in matches)
            {
                var result = match.Cancel(reason,notification.ChangedByUserId);
                if (result.IsFailure)
                    continue;

                foreach (var domainEvent in match.DomainEvents)
                {
                    var outboxMessage = OutboxMessage.Create(
                        domainEvent.GetType().AssemblyQualifiedName!,
                        JsonSerializer.Serialize(domainEvent, domainEvent.GetType()));

                    await dbContext.AddOutboxMessageAsync(outboxMessage, cancellationToken);
                }
            }

            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
