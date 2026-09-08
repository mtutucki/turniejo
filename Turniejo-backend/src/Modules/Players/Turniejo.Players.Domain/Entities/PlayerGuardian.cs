using Turniejo.Players.Domain.Enums;
using Turniejo.Players.Domain.Events;
using Turniejo.SharedKernel.Primitives;
using Turniejo.SharedKernel.Results;

namespace Turniejo.Players.Domain.Entities
{
    public sealed class PlayerGuardian : BaseEntity
    {
        public Guid PlayerId { get; private set; }
        public Guid UserId { get; private set; }
        public RelationshipType RelationshipType { get; private set; }
        public bool CanEdit { get; private set; }
        public bool CanReceiveNotifications { get; private set; }

        private readonly List<IDomainEvent> _domainEvents = [];
        public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

        private PlayerGuardian() { }
        private PlayerGuardian(Guid id) : base(id) { }

        public static Result<PlayerGuardian> Create(
            Guid id, Guid playerId, Guid userId, RelationshipType relationshipType,
            bool canEdit, bool canReceiveNotifications, Guid assignedByUserId)
        {
            if (id == Guid.Empty)
                return Result.Failure<PlayerGuardian>(Error.Validation("Id", "PlayerGuardian id cannot be empty"));

            if (playerId == Guid.Empty)
                return Result.Failure<PlayerGuardian>(Error.Validation("PlayerId", "PlayerGuardian playerId cannot be empty"));

            if (userId == Guid.Empty)
                return Result.Failure<PlayerGuardian>(Error.Validation("UserId", "PlayerGuardian userId cannot be empty"));

            var playerGuardian = new PlayerGuardian(id)
            {
                PlayerId = playerId,
                UserId = userId,
                RelationshipType = relationshipType,
                CanEdit = canEdit,
                CanReceiveNotifications = canReceiveNotifications
            };

            playerGuardian._domainEvents.Add(new GuardianAssignedToPlayer(playerGuardian.Id, playerId, userId, assignedByUserId));

            return Result.Success(playerGuardian);
        }

        public void ClearDomainEvents() => _domainEvents.Clear();
    }
}
