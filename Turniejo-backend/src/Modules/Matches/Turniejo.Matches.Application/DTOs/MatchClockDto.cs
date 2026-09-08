namespace Turniejo.Matches.Application.DTOs
{
    public sealed record MatchClockDto(Guid MatchId, string Status, int ElapsedSeconds);
}
