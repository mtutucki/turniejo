using Turniejo.Notifications.Application.Abstractions;
using Turniejo.Notifications.Domain.Enums;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Exceptions;
using Turniejo.SharedKernel.Results;

namespace Turniejo.Notifications.Application.Notifications.DeleteNotification
{
    internal sealed class DeleteNotificationCommandHandler(
        INotificationsDbContext dbContext,
        ICurrentUserContext currentUserContext) : ICommandHandler<DeleteNotificationCommand>
    {
        public async Task<Result> Handle(DeleteNotificationCommand request, CancellationToken cancellationToken)
        {
            var recipient = await dbContext.GetNotificationRecipientByIdAsync(request.NotificationRecipientId, cancellationToken);
            if (recipient == null)
                return Result.Failure(Error.NotFound("Notification", request.NotificationRecipientId));

            if (recipient.UserId != currentUserContext.UserId)
                throw new UnauthorizedException();

            if (recipient.Status != NotificationRecipientStatus.Read)
                return Result.Failure(Error.Conflict("Notification", "Only read notifications can be deleted"));

            dbContext.RemoveNotificationRecipient(recipient);
            await dbContext.SaveChangesAsync(cancellationToken);

            return Result.Success();
        }
    }
}
