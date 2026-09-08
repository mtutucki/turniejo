using Turniejo.Notifications.Application.Abstractions;
using Turniejo.Notifications.Application.DTOs;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Results;

namespace Turniejo.Notifications.Application.Notifications.GetMyNotifications
{
    internal sealed class GetMyNotificationsQueryHandler(
        INotificationsDbContext dbContext,
        ICurrentUserContext currentUserContext) : IQueryHandler<GetMyNotificationsQuery, IReadOnlyList<NotificationDto>>
    {
        public async Task<Result<IReadOnlyList<NotificationDto>>> Handle(GetMyNotificationsQuery request, CancellationToken cancellationToken)
        {
            var recipients = await dbContext.GetNotificationRecipientsByUserIdAsync(currentUserContext.UserId, cancellationToken);

            var dtos = new List<NotificationDto>();
            foreach (var recipient in recipients)
            {
                var notification = await dbContext.GetNotificationByIdAsync(recipient.NotificationId, cancellationToken);
                if (notification == null)
                    continue;

                dtos.Add(new NotificationDto(
                    recipient.Id, notification.Id, notification.TournamentId, notification.MatchId,
                    notification.Type.ToString(), notification.Title, notification.Message,
                    recipient.Status.ToString(), notification.CreatedAt, recipient.ReadAt));
            }

            var sorted = dtos.OrderByDescending(d => d.CreatedAt).ToList();

            return Result.Success<IReadOnlyList<NotificationDto>>(sorted);
        }
    }
}
