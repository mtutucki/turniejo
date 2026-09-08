namespace Turniejo.Tournaments.Application.DTOs
{
    public sealed record TournamentDashboardDto(
        Guid TournamentId,
        string TournamentName,
        string TournamentStatus,
        TeamCountsDto Teams,
        MatchCountsDto Matches,
        string ScheduleStatus,
        IReadOnlyList<UpcomingMatchDto> UpcomingMatches);

    public sealed record TeamCountsDto(
        int Pending,
        int Accepted,
        int Rejected,
        int Withdrawn,
        int Total);

    public sealed record MatchCountsDto(
        int Finished,
        int Scheduled,
        int Total);

    public sealed record UpcomingMatchDto(
        Guid MatchId,
        DateTimeOffset ScheduledAt,
        string Status,
        string HomeTeamName,
        string AwayTeamName);
}
