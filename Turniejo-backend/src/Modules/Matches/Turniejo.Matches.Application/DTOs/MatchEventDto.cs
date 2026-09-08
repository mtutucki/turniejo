namespace Turniejo.Matches.Application.DTOs
{
    public sealed record MatchEventDto(
        Guid Id,
        Guid MatchId,
        string Type,
        Guid? TournamentTeamId,
        Guid? PlayerId,
        Guid? RelatedPlayerId,
        int Minute,
        int? Second,
        Guid CreatedByUserId,
        DateTimeOffset OccurredAt);
}
