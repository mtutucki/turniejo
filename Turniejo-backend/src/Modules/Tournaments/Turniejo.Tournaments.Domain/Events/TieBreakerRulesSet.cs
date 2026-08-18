using Turniejo.SharedKernel.Primitives;
using Turniejo.Tournaments.Domain.Enums;

namespace Turniejo.Tournaments.Domain.Events
{
    public sealed record TieBreakerRuleEntry(int Order, TieBreakerType Type);

    public sealed record TieBreakerRulesSet(
        Guid TournamentId,
        Guid OrganizationId,
        Guid SetByUserId,
        IReadOnlyList<TieBreakerRuleEntry> Rules) : IOutboxEvent
    {
        public Guid EventId { get; } = Guid.NewGuid();
        public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UtcNow;
    }
}