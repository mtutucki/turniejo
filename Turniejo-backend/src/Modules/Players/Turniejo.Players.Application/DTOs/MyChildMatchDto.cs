namespace Turniejo.Players.Application.DTOs
{
    public sealed record MyChildMatchDto(
        Guid MatchId,
        Guid TournamentId,
        string TournamentName,
        DateTimeOffset ScheduledAt,
        string Status,
        string HomeTeamName,
        string AwayTeamName,
        int HomeScore,
        int AwayScore);
}
