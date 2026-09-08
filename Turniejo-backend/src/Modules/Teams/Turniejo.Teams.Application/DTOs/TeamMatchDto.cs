namespace Turniejo.Teams.Application.DTOs
{
    public sealed record TeamMatchDto(
        Guid MatchId,
        DateTimeOffset ScheduledAt,
        string Status,
        string HomeTeamName,
        string AwayTeamName,
        int HomeScore,
        int AwayScore);
}
