namespace Turniejo.SharedKernel.Contracts;

public interface IMatchModule
{
    Task<IReadOnlyList<GroupMatchDto>> GetFinishedMatchesByGroupIdAsync(Guid groupId, CancellationToken cancellationToken = default);
    Task<Guid> CreateBracketMatchAsync(
        Guid tournamentId, Guid? categoryId, Guid homeTournamentTeamId, Guid awayTournamentTeamId,
        string stageType, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TournamentMatchDto>> GetMatchesByTournamentIdAsync(Guid tournamentId, CancellationToken cancellationToken = default);
    Task<MatchInfoDto?> GetMatchAsync(Guid matchId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TournamentMatchDto>> GetMatchesByTournamentTeamIdsAsync(IReadOnlyCollection<Guid> tournamentTeamIds, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TeamMatchEventDto>> GetMatchEventsByTournamentTeamIdAsync(Guid tournamentTeamId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MatchInfoDto>> GetMatchesScheduledBetweenAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default);

    // Cross-module istnienie-check dla blokad kasowania (np. kategorii turnieju) — Match.CategoryId
    // nie ma FK, więc bez tego usunięcie kategorii cicho osierocało mecze.
    Task<bool> AnyMatchesForCategoryAsync(Guid categoryId, CancellationToken cancellationToken = default);
}

public record GroupMatchDto(
    Guid Id,
    Guid GroupId,
    Guid HomeTournamentTeamId,
    Guid AwayTournamentTeamId,
    int HomeScore,
    int AwayScore);

public record TournamentMatchDto(
    Guid Id,
    Guid TournamentId,
    string Status,
    DateTimeOffset ScheduledAt,
    Guid HomeTournamentTeamId,
    Guid AwayTournamentTeamId,
    int HomeScore,
    int AwayScore);

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
