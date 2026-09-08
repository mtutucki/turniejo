namespace Turniejo.Matches.Application.DTOs
{
    public sealed record MatchTimelineEntryDto(
        Guid Id,
        string Type,
        int Minute,
        int? Second,
        string? TeamName,
        string? PlayerName,
        string? RelatedPlayerName);
}
