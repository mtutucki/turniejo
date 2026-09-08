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
    Task<PublicTournamentDto?> GetPublicTournamentBySlugAsync(string slug, CancellationToken cancellationToken = default);
    Task<TournamentPublicSettingsDto?> GetTournamentPublicSettingsAsync(Guid tournamentId, CancellationToken cancellationToken = default);
}

public record TournamentDto(Guid Id, string Name, string Status, string? Format, Guid OrganizationId, int TeamLimit);
public record TournamentCategoryDto(Guid Id, Guid TournamentId, string Name, DateTime? MinBirthDate, DateTime? MaxBirthDate);
public record TournamentGroupDto(Guid Id, Guid TournamentId, Guid CategoryId, string Name);
public record TournamentSettingsSummaryDto(
    Guid TournamentId, int MatchDurationMinutes, int BreakDurationMinutes, int RefereeEditWindowMinutes,
    int PointsForWin, int PointsForDraw, int PointsForLoss, bool IsThirdPlaceMatchEnabled,
    bool CanCoachEditSquadAfterStart, int WalkoverWinnerScore, int WalkoverLoserScore);
public record TieBreakerRuleDto(int Order, string Type);
public record TournamentPublicSettingsDto(
    Guid TournamentId, bool IsPublic, bool ShowTeams, bool ShowPlayers,
    bool ShowSchedule, bool ShowStandings, bool ShowBracket, bool ShowLiveMatches);
public record PublicTournamentDto(
    Guid Id, string Name, string Slug, string Status, string? Format,
    DateTimeOffset StartAt, DateTimeOffset EndAt, string? Description,
    TournamentPublicSettingsDto Settings);
