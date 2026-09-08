using Turniejo.Notifications.Domain.Enums;
using Turniejo.Notifications.Domain.Events;
using Turniejo.SharedKernel.Primitives;
using Turniejo.SharedKernel.Results;

namespace Turniejo.Notifications.Domain.Entities
{
    public sealed class Notification : BaseEntity
    {
        public Guid? TournamentId { get; private set; }
        public Guid? MatchId { get; private set; }
        public NotificationType Type { get; private set; }
        public string Title { get; private set; } = string.Empty;
        public string Message { get; private set; } = string.Empty;
        public Guid? CreatedByUserId { get; private set; }

        private readonly List<IDomainEvent> _domainEvents = [];
        public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

        private Notification() { }
        private Notification(Guid id) : base(id) { }

        public static Result<Notification> Create(
            Guid id, Guid? tournamentId, NotificationType type, string title, string message,
            Guid? createdByUserId, Guid? matchId = null)
        {
            if (id == Guid.Empty)
                return Result.Failure<Notification>(Error.Validation("Id", "Notification id cannot be empty"));

            if (string.IsNullOrWhiteSpace(title))
                return Result.Failure<Notification>(Error.Validation("Title", "Notification title cannot be empty"));

            if (string.IsNullOrWhiteSpace(message))
                return Result.Failure<Notification>(Error.Validation("Message", "Notification message cannot be empty"));

            var notification = new Notification(id)
            {
                TournamentId = tournamentId,
                MatchId = matchId,
                Type = type,
                Title = title,
                Message = message,
                CreatedByUserId = createdByUserId
            };

            notification._domainEvents.Add(new NotificationCreated(notification.Id, tournamentId, type.ToString()));

            return Result.Success(notification);
        }

        public void ClearDomainEvents() => _domainEvents.Clear();
    }
}
