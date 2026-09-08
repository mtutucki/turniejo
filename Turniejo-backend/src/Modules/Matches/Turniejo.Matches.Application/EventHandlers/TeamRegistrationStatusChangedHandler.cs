using System.Text.Json;
using MediatR;
using Turniejo.Matches.Application.Abstractions;
using Turniejo.SharedKernel.Outbox;
using Turniejo.Teams.Domain.Enums;
using Turniejo.Teams.Domain.Events;

namespace Turniejo.Matches.Application.EventHandlers
{
    // Wycofanie drużyny z turnieju nie blokowało startu/zakończenia jej jeszcze nierozegranych
    // meczów — sędzia mógł dalej prowadzić mecz drużyny, która się wycofała.
    internal sealed class TeamRegistrationStatusChangedHandler(IMatchesDbContext dbContext) : INotificationHandler<TeamRegistrationStatusChanged>
    {
        public async Task Handle(TeamRegistrationStatusChanged notification, CancellationToken cancellationToken)
        {
            if (notification.NewStatus != RegistrationStatus.Withdrawn)
                return;

            var matches = await dbContext.GetCancellableMatchesByTournamentTeamIdAsync(notification.TournamentTeamId, cancellationToken);
            if (matches.Count == 0)
                return;

            foreach (var match in matches)
            {
                var result = match.Cancel("Drużyna wycofała się z turnieju.", notification.ChangedByUserId);
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
