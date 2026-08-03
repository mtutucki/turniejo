using Turniejo.SharedKernel.Primitives;

namespace Turniejo.Tournaments.Domain.Events;

public sealed record TournamentCreated(
    Guid TournamentId,
    Guid OrganizationId,
    Guid CreatedByUserId,
    string Name,
    DateTimeOffset StartAt,
    DateTimeOffset EndAt) : IOutboxEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UtcNow;
}
