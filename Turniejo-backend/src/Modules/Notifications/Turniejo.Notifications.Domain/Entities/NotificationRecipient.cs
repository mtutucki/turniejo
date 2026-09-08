using Turniejo.Notifications.Domain.Enums;
using Turniejo.Notifications.Domain.Events;
using Turniejo.SharedKernel.Primitives;
using Turniejo.SharedKernel.Results;

namespace Turniejo.Notifications.Domain.Entities
{
    public sealed class NotificationRecipient : BaseEntity
    {
        public Guid NotificationId { get; private set; }
        public Guid UserId { get; private set; }
        public NotificationRecipientStatus Status { get; private set; } = NotificationRecipientStatus.Pending;
        public DateTimeOffset? ReadAt { get; private set; }

        private readonly List<IDomainEvent> _domainEvents = [];
        public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

        private NotificationRecipient() { }
        private NotificationRecipient(Guid id) : base(id) { }

        public static Result<NotificationRecipient> Create(Guid id, Guid notificationId, Guid userId)
        {
            if (id == Guid.Empty)
                return Result.Failure<NotificationRecipient>(Error.Validation("Id", "NotificationRecipient id cannot be empty"));

            if (notificationId == Guid.Empty)
                return Result.Failure<NotificationRecipient>(Error.Validation("NotificationId", "NotificationRecipient notificationId cannot be empty"));

            if (userId == Guid.Empty)
                return Result.Failure<NotificationRecipient>(Error.Validation("UserId", "NotificationRecipient userId cannot be empty"));

            var recipient = new NotificationRecipient(id)
            {
                NotificationId = notificationId,
                UserId = userId
            };

            return Result.Success(recipient);
        }

        public Result MarkAsRead()
        {
            if (Status == NotificationRecipientStatus.Read)
                return Result.Success();

            Status = NotificationRecipientStatus.Read;
            ReadAt = DateTimeOffset.UtcNow;
            SetUpdatedAt();

            _domainEvents.Add(new NotificationRead(Id, NotificationId, UserId));

            return Result.Success();
        }

        public void ClearDomainEvents() => _domainEvents.Clear();
    }
}
