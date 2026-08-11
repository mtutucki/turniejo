using Turniejo.SharedKernel.Primitives;

namespace Turniejo.Tournaments.Domain.Events
{
    public sealed record TournamentUpdated(
        Guid TournamentId,
        Guid OrganizationId,
        string Name,
        DateTimeOffset StartAt,
        DateTimeOffset EndAt,
        int TeamLimit,
        string? Description) : IOutboxEvent
    {
        public Guid EventId { get; } = Guid.NewGuid();
        public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UtcNow;
    }
}