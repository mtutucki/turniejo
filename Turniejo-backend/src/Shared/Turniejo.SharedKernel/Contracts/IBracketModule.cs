namespace Turniejo.SharedKernel.Contracts;

// Mecz kolejnej rundy drabinki powstaje dopiero, gdy znane są obie drużyny (Match wymaga obu), więc
// półfinał czy finał nie miały terminu aż do rozstrzygnięcia poprzedniej rundy. Generator terminarza
// (moduł Matches) planuje im termin z góry i zapisuje go na węźle drabinki; mecz powstaje potem w tym
// terminie (BracketAdvancer -> IMatchModule.CreateBracketMatchAsync).
public interface IBracketModule
{
    Task<IReadOnlyList<BracketNodeInfoDto>> GetNodesByTournamentIdAsync(Guid tournamentId, CancellationToken cancellationToken = default);
    Task SetPlannedSlotsAsync(IReadOnlyCollection<BracketPlannedSlotDto> slots, CancellationToken cancellationToken = default);
    /// <summary>Po wyczyszczeniu terminarza: terminy kolejnych rund pucharu i zaplanowane mecze fazy 2 (z obsadą miejsc).</summary>
    Task ClearPlannedSlotsAsync(Guid tournamentId, CancellationToken cancellationToken = default);
    Task SetPlannedRefereeAsync(Guid nodeId, Guid? refereeId, CancellationToken cancellationToken = default);

    // Format „Dwie fazy grupowe”: miejsca w grupach fazy 2, obsadzane z tabel fazy 1.
    Task<IReadOnlyList<SecondStageSeatInfoDto>> GetSecondStageSeatsAsync(Guid tournamentId, CancellationToken cancellationToken = default);
    /// <summary>Usunięta grupa (fazy 1 albo 2) nie może zostać w podziale drugiej fazy.</summary>
    Task RemoveSecondStageSeatsForGroupAsync(Guid groupId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SecondStageFixtureInfoDto>> GetSecondStageFixturesAsync(Guid tournamentId, CancellationToken cancellationToken = default);
    /// <summary>Zaplanowane przez generator mecze fazy 2 (zastępują poprzednie). Czyści je <see cref="ClearPlannedSlotsAsync"/>.</summary>
    Task CreateSecondStageFixturesAsync(Guid tournamentId, IReadOnlyCollection<SecondStageFixturePlanDto> fixtures, CancellationToken cancellationToken = default);
}

public record SecondStageFixtureInfoDto(
    Guid Id, Guid CategoryId, Guid GroupId, Guid HomeSeatId, Guid AwaySeatId, int Round,
    Guid? PlannedPitchId, DateTimeOffset? PlannedAt, Guid? PlannedRefereeId, Guid? MatchId);

public record SecondStageFixturePlanDto(
    Guid Id, Guid CategoryId, Guid GroupId, Guid HomeSeatId, Guid AwaySeatId, int Round, Guid PitchId, DateTimeOffset ScheduledAt);

public record SecondStageSeatInfoDto(
    Guid Id, Guid CategoryId, Guid GroupId, int Order, Guid SourceGroupId, int SourcePosition, Guid? TournamentTeamId);

// HasBye: węzeł z wolnym losem nigdy nie dostaje meczu, więc nie potrzebuje terminu.
// CategoryId, grupy źródłowe i znane drużyny: generator terminarza planuje mecz pucharu po fazie grupowej
// jego grup i pilnuje, żeby drużyna grająca w dwóch kategoriach nie miała dwóch meczów naraz.
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
    Guid? PlannedRefereeId = null,
    Guid CategoryId = default,
    Guid? HomeSourceGroupId = null,
    Guid? AwaySourceGroupId = null,
    Guid? HomeTournamentTeamId = null,
    Guid? AwayTournamentTeamId = null);

public record BracketPlannedSlotDto(Guid NodeId, Guid PitchId, DateTimeOffset ScheduledAt);
