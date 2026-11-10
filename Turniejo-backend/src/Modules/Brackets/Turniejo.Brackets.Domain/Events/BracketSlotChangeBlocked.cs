using Turniejo.SharedKernel.Primitives;

namespace Turniejo.Brackets.Domain.Events
{
    /// <summary>
    /// Korekta wyniku w grupie zmieniła kolejność w tabeli, ale mecz pucharowy z drużyną z tego miejsca
    /// już się zaczął, więc drużyna w nim została stara (QA v3: GPK-008). Organizator musi o tym wiedzieć.
    /// </summary>
    /// <param name="SecondStage">Mecz grupy drugiej fazy (format „Dwie fazy grupowe”), a nie pucharu.</param>
    public sealed record BracketSlotChangeBlocked(Guid TournamentId, IReadOnlyList<Guid> MatchIds, bool SecondStage = false) : IOutboxEvent
    {
        public Guid EventId { get; } = Guid.NewGuid();
        public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UtcNow;
    }
}
