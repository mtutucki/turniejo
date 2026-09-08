namespace Turniejo.Matches.Application.DTOs
{
    public sealed record MatchSummaryDto(
        Guid MatchId,
        Guid TournamentId,
        string Status,
        string HomeTeamName,
        string AwayTeamName,
        int HomeScore,
        int AwayScore,
        string ResultType,
        Guid? WinnerTournamentTeamId,
        DateTimeOffset? FinishedAt);
}
