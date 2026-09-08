using System.Text.Json;
using Turniejo.Notifications.Application.Abstractions;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Exceptions;
using Turniejo.SharedKernel.Outbox;
using Turniejo.SharedKernel.Results;

namespace Turniejo.Notifications.Application.Notifications.MarkNotificationAsRead
{
    internal sealed class MarkNotificationAsReadCommandHandler(
        INotificationsDbContext dbContext,
        ICurrentUserContext currentUserContext) : ICommandHandler<MarkNotificationAsReadCommand, MarkNotificationAsReadResponse>
    {
        public async Task<Result<MarkNotificationAsReadResponse>> Handle(MarkNotificationAsReadCommand request, CancellationToken cancellationToken)
        {
            var recipient = await dbContext.GetNotificationRecipientByIdAsync(request.NotificationRecipientId, cancellationToken);
            if (recipient == null)
                return Result.Failure<MarkNotificationAsReadResponse>(Error.NotFound("Notification", request.NotificationRecipientId));

            if (recipient.UserId != currentUserContext.UserId)
                throw new UnauthorizedException();

            var markResult = recipient.MarkAsRead();
            if (markResult.IsFailure)
                return Result.Failure<MarkNotificationAsReadResponse>(markResult.Error);

            foreach (var domainEvent in recipient.DomainEvents)
            {
                var outboxMessage = OutboxMessage.Create(
                    domainEvent.GetType().AssemblyQualifiedName!,
                    JsonSerializer.Serialize(domainEvent, domainEvent.GetType()));

                await dbContext.AddOutboxMessageAsync(outboxMessage, cancellationToken);
            }

            await dbContext.SaveChangesAsync(cancellationToken);

            return Result.Success(new MarkNotificationAsReadResponse(recipient.Id));
        }
    }
}
