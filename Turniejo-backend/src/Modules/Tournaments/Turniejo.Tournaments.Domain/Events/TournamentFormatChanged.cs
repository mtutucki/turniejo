using Turniejo.SharedKernel.Primitives;
using Turniejo.Tournaments.Domain.Enums;

namespace Turniejo.Tournaments.Domain.Events
{
    public sealed record TournamentFormatChanged(
        Guid TournamentId,
        Guid OrganizationId,
        TournamentFormat Format) : IOutboxEvent
    {
        public Guid EventId { get; } = Guid.NewGuid();
        public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UtcNow;
    }
}