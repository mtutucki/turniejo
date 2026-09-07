using Turniejo.SharedKernel.Primitives;
using Turniejo.Teams.Domain.Enums;

namespace Turniejo.Teams.Domain.Events
{
    public sealed record TeamPaymentStatusChanged(
        Guid TournamentTeamId,
        Guid TournamentId,
        PaymentStatus OldStatus,
        PaymentStatus NewStatus,
        Guid ChangedByUserId) : IOutboxEvent
    {
        public Guid EventId { get; } = Guid.NewGuid();
        public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UtcNow;
    }
}
