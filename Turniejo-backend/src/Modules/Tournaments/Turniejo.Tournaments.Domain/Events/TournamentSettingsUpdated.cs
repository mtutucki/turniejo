using Turniejo.SharedKernel.Primitives;

namespace Turniejo.Tournaments.Domain.Events
{
    public sealed record TournamentSettingsUpdated(
        Guid TournamentId,
        Guid OrganizationId,
        Guid ChangedByUserId) : IOutboxEvent
    {
        public Guid EventId { get; } = Guid.NewGuid();
        public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UtcNow;
    }
}