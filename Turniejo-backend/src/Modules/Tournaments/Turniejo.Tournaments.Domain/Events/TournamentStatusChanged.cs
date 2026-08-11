using Turniejo.SharedKernel.Primitives;
using Turniejo.Tournaments.Domain.Enums;

namespace Turniejo.Tournaments.Domain.Events
{
    public sealed record TournamentStatusChanged(
        Guid TournamentId,
        Guid OrganizationId,
        Guid ChangedByUserId,
        TournamentStatus OldStatus,
        TournamentStatus NewStatus) : IOutboxEvent
    {
        public Guid EventId { get; } = Guid.NewGuid();
        public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UtcNow;
    }
}
