using System.Text.Json;
using MediatR;
using Turniejo.Matches.Application.Abstractions;
using Turniejo.Matches.Domain.Enums;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.Outbox;
using Turniejo.Teams.Domain.Enums;
using Turniejo.Teams.Domain.Events;

namespace Turniejo.Matches.Application.EventHandlers
{
    // Wycofanie drużyny z turnieju nie blokowało startu/zakończenia jej jeszcze nierozegranych
    // meczów — sędzia mógł dalej prowadzić mecz drużyny, która się wycofała.
    // W trwającym turnieju mecz pucharowy dostaje walkower dla rywala zamiast odwołania: odwołany mecz
    // nie ma zwycięzcy, więc drabinka stawała (slot w kolejnej rundzie nigdy się nie wypełniał). Przed
    // startem turnieju zostaje odwołanie — organizator układa wtedy drabinkę od nowa (ResetBracket).
    internal sealed class TeamRegistrationStatusChangedHandler(
        IMatchesDbContext dbContext,
        ITournamentModule tournamentModule) : INotificationHandler<TeamRegistrationStatusChanged>
    {
        public async Task Handle(TeamRegistrationStatusChanged notification, CancellationToken cancellationToken)
        {
            if (notification.NewStatus != RegistrationStatus.Withdrawn)
                return;

            var matches = await dbContext.GetCancellableMatchesByTournamentTeamIdAsync(notification.TournamentTeamId, cancellationToken);
            if (matches.Count == 0)
                return;

            TournamentSettingsSummaryDto? walkoverSettings = null;
            if (matches.Any(m => m.StageType != StageType.Group))
            {
                var tournament = await tournamentModule.GetTournamentAsync(notification.TournamentId, cancellationToken);
                if (tournament?.Status == "InProgress")
                    walkoverSettings = await tournamentModule.GetTournamentSettingsAsync(notification.TournamentId, cancellationToken);
            }

            foreach (var match in matches)
            {
                var result = walkoverSettings != null && match.StageType != StageType.Group
                    ? match.AwardWalkover(
                        match.HomeTournamentTeamId == notification.TournamentTeamId ? match.AwayTournamentTeamId : match.HomeTournamentTeamId,
                        walkoverSettings.WalkoverWinnerScore, walkoverSettings.WalkoverLoserScore, notification.ChangedByUserId)
                    : match.Cancel("Drużyna wycofała się z turnieju.", notification.ChangedByUserId);
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
