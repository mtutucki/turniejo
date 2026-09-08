namespace Turniejo.Matches.Application.DTOs
{
    public sealed record MatchLocationDto(
        Guid MatchId,
        DateTimeOffset ScheduledAt,
        Guid? PitchId,
        string? PitchName,
        string? PitchDescription);
}
