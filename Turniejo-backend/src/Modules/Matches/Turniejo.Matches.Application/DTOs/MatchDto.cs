namespace Turniejo.Matches.Application.DTOs
{
    public sealed record MatchDto(
        Guid Id,
        Guid TournamentId,
        Guid? CategoryId,
        Guid? GroupId,
        Guid? PitchId,
        Guid HomeTournamentTeamId,
        Guid AwayTournamentTeamId,
        Guid? RefereeId,
        DateTimeOffset ScheduledAt,
        string Status,
        string StageType,
        int HomeScore,
        int AwayScore,
        string ResultType);
}
