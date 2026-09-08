using Turniejo.Notifications.Application.Abstractions;
using Turniejo.Notifications.Application.Services;
using Turniejo.Notifications.Domain.Enums;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Results;

namespace Turniejo.Notifications.Application.Notifications.CreateMatchReminderNotifications
{
    internal sealed class CreateMatchReminderNotificationsCommandHandler(
        INotificationsDbContext dbContext,
        NotificationPublisher publisher,
        MatchNotificationRecipientResolver recipientResolver,
        IMatchModule matchModule,
        ITeamModule teamModule) : ICommandHandler<CreateMatchReminderNotificationsCommand, CreateMatchReminderNotificationsResponse>
    {
        public async Task<Result<CreateMatchReminderNotificationsResponse>> Handle(CreateMatchReminderNotificationsCommand request, CancellationToken cancellationToken)
        {
            var now = DateTimeOffset.UtcNow;
            var matches = await matchModule.GetMatchesScheduledBetweenAsync(now, now.AddMinutes(request.MinutesBeforeMatch), cancellationToken);

            var createdCount = 0;
            foreach (var match in matches)
            {
                var alreadyReminded = await dbContext.MatchReminderExistsAsync(match.Id, cancellationToken);
                if (alreadyReminded)
                    continue;

                var recipients = await recipientResolver.ResolveAsync(
                    match.RefereeId, match.HomeTournamentTeamId, match.AwayTournamentTeamId, cancellationToken);
                if (recipients.Count == 0)
                    continue;

                var homeTeamName = await GetTeamNameAsync(match.HomeTournamentTeamId, cancellationToken);
                var awayTeamName = await GetTeamNameAsync(match.AwayTournamentTeamId, cancellationToken);

                var title = "Przypomnienie o meczu";
                var message = $"Mecz {homeTeamName} vs {awayTeamName} rozpocznie się o {match.ScheduledAt:HH:mm}.";

                var publishResult = await publisher.PublishAsync(
                    match.TournamentId, NotificationType.MatchReminder, title, message,
                    createdByUserId: null, recipients, matchId: match.Id, cancellationToken);

                if (publishResult.IsSuccess)
                    createdCount++;
            }

            if (createdCount > 0)
                await dbContext.SaveChangesAsync(cancellationToken);

            return Result.Success(new CreateMatchReminderNotificationsResponse(createdCount));
        }

        private async Task<string> GetTeamNameAsync(Guid tournamentTeamId, CancellationToken cancellationToken)
        {
            var tournamentTeam = await teamModule.GetTournamentTeamAsync(tournamentTeamId, cancellationToken);
            if (tournamentTeam == null)
                return "Unknown";

            var team = await teamModule.GetTeamAsync(tournamentTeam.TeamId, cancellationToken);
            return team?.Name ?? "Unknown";
        }
    }
}
