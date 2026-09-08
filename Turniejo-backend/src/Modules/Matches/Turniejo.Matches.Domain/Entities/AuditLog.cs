using Turniejo.SharedKernel.Primitives;
using Turniejo.SharedKernel.Results;

namespace Turniejo.Matches.Domain.Entities
{
    public sealed class AuditLog : BaseEntity
    {
        public Guid? UserId { get; private set; }
        public string EntityType { get; private set; } = string.Empty;
        public Guid EntityId { get; private set; }
        public string Action { get; private set; } = string.Empty;
        public string? OldValue { get; private set; }
        public string? NewValue { get; private set; }

        private AuditLog() { }
        private AuditLog(Guid id) : base(id) { }

        public static Result<AuditLog> Create(
            Guid id, Guid? userId, string entityType, Guid entityId, string action, string? oldValue, string? newValue)
        {
            if (id == Guid.Empty)
                return Result.Failure<AuditLog>(Error.Validation("Id", "AuditLog id cannot be empty"));

            if (string.IsNullOrWhiteSpace(entityType))
                return Result.Failure<AuditLog>(Error.Validation("EntityType", "AuditLog entityType cannot be empty"));

            if (entityId == Guid.Empty)
                return Result.Failure<AuditLog>(Error.Validation("EntityId", "AuditLog entityId cannot be empty"));

            if (string.IsNullOrWhiteSpace(action))
                return Result.Failure<AuditLog>(Error.Validation("Action", "AuditLog action cannot be empty"));

            var auditLog = new AuditLog(id)
            {
                UserId = userId,
                EntityType = entityType,
                EntityId = entityId,
                Action = action,
                OldValue = oldValue,
                NewValue = newValue
            };

            return Result.Success(auditLog);
        }
    }
}
