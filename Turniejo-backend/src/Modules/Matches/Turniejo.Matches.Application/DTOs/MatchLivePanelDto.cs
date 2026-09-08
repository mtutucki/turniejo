namespace Turniejo.Matches.Application.DTOs
{
    public sealed record MatchPlayerDto(
        Guid TournamentPlayerId,
        Guid PlayerId,
        string FirstName,
        string LastName,
        int? ShirtNumber,
        string? Position,
        string Status);

    public sealed record MatchTeamDto(
        Guid TournamentTeamId,
        Guid TeamId,
        string Name,
        string? ShortName,
        IReadOnlyList<MatchPlayerDto> Players);

    public sealed record MatchLivePanelDto(
        Guid MatchId,
        Guid TournamentId,
        string Status,
        string StageType,
        DateTimeOffset ScheduledAt,
        DateTimeOffset? StartedAt,
        DateTimeOffset? PausedAt,
        DateTimeOffset? FinishedAt,
        int TotalPausedSeconds,
        int HomeScore,
        int AwayScore,
        string ResultType,
        Guid? WinnerTournamentTeamId,
        MatchTeamDto HomeTeam,
        MatchTeamDto AwayTeam,
        IReadOnlyList<MatchEventDto> Events);
}
