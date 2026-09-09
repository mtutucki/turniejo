using Turniejo.Notifications.Application.Abstractions;
using Turniejo.Notifications.Application.Services;
using Turniejo.Notifications.Domain.Enums;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Results;

namespace Turniejo.Notifications.Application.Notifications.CreateOrganizerAnnouncement
{
    internal sealed class CreateOrganizerAnnouncementCommandHandler(
        INotificationsDbContext dbContext,
        NotificationPublisher publisher,
        TournamentParticipantResolver participantResolver,
        ICurrentUserContext currentUserContext) : ICommandHandler<CreateOrganizerAnnouncementCommand, CreateOrganizerAnnouncementResponse>
    {
        public async Task<Result<CreateOrganizerAnnouncementResponse>> Handle(CreateOrganizerAnnouncementCommand request, CancellationToken cancellationToken)
        {
            var recipientUserIds = await participantResolver.ResolveAsync(request.TournamentId, cancellationToken);

            var publishResult = await publisher.PublishAsync(
                request.TournamentId, NotificationType.OrganizerAnnouncement, request.Title, request.Message,
                currentUserContext.UserId, recipientUserIds, matchId: null, cancellationToken);

            if (publishResult.IsFailure)
                return Result.Failure<CreateOrganizerAnnouncementResponse>(publishResult.Error);

            await dbContext.SaveChangesAsync(cancellationToken);

            return Result.Success(new CreateOrganizerAnnouncementResponse(publishResult.Value.Id, recipientUserIds.Count));
        }
    }
}
