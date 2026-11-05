namespace Turniejo.SharedKernel.Contracts;

// Mecz kolejnej rundy drabinki powstaje dopiero, gdy znane są obie drużyny (Match wymaga obu), więc
// półfinał czy finał nie miały terminu aż do rozstrzygnięcia poprzedniej rundy. Generator terminarza
// (moduł Matches) planuje im termin z góry i zapisuje go na węźle drabinki; mecz powstaje potem w tym
// terminie (BracketAdvancer -> IMatchModule.CreateBracketMatchAsync).
public interface IBracketModule
{
    Task<IReadOnlyList<BracketNodeInfoDto>> GetNodesByTournamentIdAsync(Guid tournamentId, CancellationToken cancellationToken = default);
    Task SetPlannedSlotsAsync(IReadOnlyCollection<BracketPlannedSlotDto> slots, CancellationToken cancellationToken = default);
    Task ClearPlannedSlotsAsync(Guid tournamentId, CancellationToken cancellationToken = default);
    Task SetPlannedRefereeAsync(Guid nodeId, Guid? refereeId, CancellationToken cancellationToken = default);
}

// HasBye: węzeł z wolnym losem nigdy nie dostaje meczu, więc nie potrzebuje terminu.
public record BracketNodeInfoDto(
    Guid Id,
    int RoundNumber,
    bool IsThirdPlace,
    Guid? MatchId,
    Guid? HomeSourceNodeId,
    Guid? AwaySourceNodeId,
    bool HasBye,
    Guid? PlannedPitchId = null,
    DateTimeOffset? PlannedAt = null,
    Guid? PlannedRefereeId = null);

public record BracketPlannedSlotDto(Guid NodeId, Guid PitchId, DateTimeOffset ScheduledAt);
