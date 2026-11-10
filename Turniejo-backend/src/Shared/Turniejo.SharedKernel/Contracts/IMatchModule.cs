namespace Turniejo.SharedKernel.Contracts;

public interface IMatchModule
{
    /// <summary>Liczba trwających meczów (na żywo albo w przerwie) w każdym z turniejów. Turniej bez nich ma 0.</summary>
    Task<IReadOnlyDictionary<Guid, int>> CountLiveMatchesByTournamentIdsAsync(IReadOnlyCollection<Guid> tournamentIds, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GroupMatchDto>> GetFinishedMatchesByGroupIdAsync(Guid groupId, CancellationToken cancellationToken = default);
    Task<Guid> CreateBracketMatchAsync(
        Guid tournamentId, Guid? categoryId, Guid homeTournamentTeamId, Guid awayTournamentTeamId,
        string stageType, Guid? plannedPitchId, DateTimeOffset? plannedAt, Guid? plannedRefereeId = null,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TournamentMatchDto>> GetMatchesByTournamentIdAsync(Guid tournamentId, CancellationToken cancellationToken = default);
    Task<MatchInfoDto?> GetMatchAsync(Guid matchId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PitchInfoDto>> GetPitchesByTournamentIdAsync(Guid tournamentId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TournamentMatchDto>> GetMatchesByTournamentTeamIdsAsync(IReadOnlyCollection<Guid> tournamentTeamIds, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TeamMatchEventDto>> GetMatchEventsByTournamentTeamIdAsync(Guid tournamentTeamId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MatchInfoDto>> GetMatchesScheduledBetweenAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default);

    // Cross-module istnienie-check dla blokad kasowania (np. kategorii turnieju) — Match.CategoryId
    // nie ma FK, więc bez tego usunięcie kategorii cicho osierocało mecze.
    Task<bool> AnyMatchesForCategoryAsync(Guid categoryId, CancellationToken cancellationToken = default);
    // Jak wyżej, dla Match.GroupId — blokada kasowania grupy turnieju.
    Task<bool> AnyMatchesForGroupAsync(Guid groupId, CancellationToken cancellationToken = default);
    // Grupa ma mecze i wszystkie (poza odwołanymi) są zakończone — dopiero wtedy miejsca w tabeli są
    // ostateczne i drabinka (moduł Brackets) może obsadzić sloty "N. miejsce grupy".
    Task<bool> IsGroupStageFinishedAsync(Guid groupId, CancellationToken cancellationToken = default);

    // Reset drabinki (moduł Brackets): usuwa jej mecze, o ile żaden się jeszcze nie zaczął. Zwraca false
    // i nic nie usuwa, gdy któryś jest w trakcie albo zakończony.
    Task<bool> RemoveUnstartedMatchesAsync(IReadOnlyCollection<Guid> matchIds, CancellationToken cancellationToken = default);

    // Zamiana par w drabince (moduł Brackets): podmienia drużyny meczu, który się jeszcze nie zaczął —
    // termin, boisko i sędzia zostają. Zwraca false i nic nie zmienia, gdy meczu nie ma albo już ruszył.
    Task<bool> ChangeUnstartedMatchTeamsAsync(Guid matchId, Guid homeTournamentTeamId, Guid awayTournamentTeamId, CancellationToken cancellationToken = default);

    // Ręczne przeliczenie drabinki (moduł Brackets): ustawia zwycięzcę zakończonych meczów pucharowych
    // zgodnie z ich wynikiem i zwraca go (mecz id -> zwycięzca). Mecze niezakończone są pomijane.
    Task<IReadOnlyDictionary<Guid, Guid?>> RefreshKnockoutWinnersAsync(IReadOnlyCollection<Guid> matchIds, CancellationToken cancellationToken = default);
}

public record GroupMatchDto(
    Guid Id,
    Guid GroupId,
    Guid HomeTournamentTeamId,
    Guid AwayTournamentTeamId,
    int HomeScore,
    int AwayScore);

// Pola od StageType opcjonalne, żeby nie przepisywać istniejących wywołań. Karne rozstrzygają remis
// w pucharze (historia drużyny liczy z nich zwycięstwo albo porażkę).
public record TournamentMatchDto(
    Guid Id,
    Guid TournamentId,
    string Status,
    DateTimeOffset ScheduledAt,
    Guid HomeTournamentTeamId,
    Guid AwayTournamentTeamId,
    int HomeScore,
    int AwayScore,
    string StageType = "Group",
    Guid? GroupId = null,
    Guid? CategoryId = null,
    int? HomePenaltyScore = null,
    int? AwayPenaltyScore = null);

public record MatchInfoDto(
    Guid Id,
    Guid TournamentId,
    Guid HomeTournamentTeamId,
    Guid AwayTournamentTeamId,
    Guid? RefereeId,
    Guid? PitchId,
    DateTimeOffset ScheduledAt,
    string Status);

public record TeamMatchEventDto(
    Guid MatchId,
    string Type,
    Guid? PlayerId,
    Guid? RelatedPlayerId);

public record PitchInfoDto(Guid Id, string Name, bool IsActive);
