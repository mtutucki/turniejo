namespace Turniejo.SharedKernel.Contracts;

public interface ITournamentModule
{
    Task<TournamentDto?> GetTournamentAsync(Guid tournamentId, CancellationToken cancellationToken = default);
    Task<TournamentCategoryDto?> GetTournamentCategoryAsync(Guid categoryId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TournamentGroupDto>> GetTournamentGroupsAsync(Guid tournamentId, CancellationToken cancellationToken = default);
    Task<TournamentGroupDto?> GetTournamentGroupAsync(Guid groupId, CancellationToken cancellationToken = default);
    Task<TournamentSettingsSummaryDto?> GetTournamentSettingsAsync(Guid tournamentId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TieBreakerRuleDto>> GetTieBreakerRulesAsync(Guid tournamentId, CancellationToken cancellationToken = default);
    Task<bool> ChangeTournamentStatusAsync(Guid tournamentId, string newStatus, CancellationToken cancellationToken = default);
    /// <summary>ScheduleGenerated -> RegistrationClosed po wyczyszczeniu terminarza (poza zwykłą maszyną stanów).</summary>
    Task<bool> RevertScheduleGenerationAsync(Guid tournamentId, CancellationToken cancellationToken = default);
    /// <summary>
    /// ScheduleGenerated -> InProgress przy starcie pierwszego meczu. Bez autoryzacji organizatora, bo mecz
    /// startuje sędzia — zmiana wynika z faktu (mecz trwa), a nie z decyzji użytkownika.
    /// </summary>
    Task<bool> MarkTournamentStartedAsync(Guid tournamentId, Guid changedByUserId, CancellationToken cancellationToken = default);
    Task<PublicTournamentDto?> GetPublicTournamentBySlugAsync(string slug, CancellationToken cancellationToken = default);
    Task<TournamentPublicSettingsDto?> GetTournamentPublicSettingsAsync(Guid tournamentId, CancellationToken cancellationToken = default);
    /// <summary>
    /// Usunięcie organizacji: miękko usuwa wszystkie jej turnieje niezależnie od statusu (zwykłe usuwanie
    /// wymaga anulowania). Zwraca Id wszystkich turniejów organizacji, także usuniętych wcześniej.
    /// </summary>
    Task<IReadOnlyList<Guid>> DeleteTournamentsByOrganizationIdAsync(Guid organizationId, CancellationToken cancellationToken = default);
}

// StartAt/EndAt opcjonalne w konstruktorze, żeby nie przepisywać wszystkich istniejących wywołań.
public record TournamentDto(Guid Id, string Name, string Status, string? Format, Guid OrganizationId, int TeamLimit,
    DateTimeOffset? StartAt = null, DateTimeOffset? EndAt = null);
public record TournamentCategoryDto(Guid Id, Guid TournamentId, string Name, DateTime? MinBirthDate, DateTime? MaxBirthDate);
public record TournamentGroupDto(Guid Id, Guid TournamentId, Guid CategoryId, string Name);
public record TournamentSettingsSummaryDto(
    Guid TournamentId, int MatchDurationMinutes, int BreakDurationMinutes, int RefereeEditWindowMinutes,
    int PointsForWin, int PointsForDraw, int PointsForLoss, bool IsThirdPlaceMatchEnabled,
    bool CanCoachEditSquadAfterStart, int WalkoverWinnerScore, int WalkoverLoserScore);
public record TieBreakerRuleDto(int Order, string Type);
public record TournamentPublicSettingsDto(
    Guid TournamentId, bool IsPublic, bool ShowTeams, bool ShowPlayers,
    bool ShowSchedule, bool ShowStandings, bool ShowBracket, bool ShowLiveMatches, bool ShowStats,
    bool AbbreviatePlayerNames = true);
public record PublicTournamentDto(
    Guid Id, string Name, string Slug, string Status, string? Format,
    DateTimeOffset StartAt, DateTimeOffset EndAt, string? Description,
    TournamentPublicSettingsDto Settings);
