namespace Turniejo.Players.Application.DTOs
{
    public sealed record PublicPlayerProfileDto(
        Guid TournamentPlayerId,
        string FirstName,
        string LastName,
        string? PhotoUrl,
        int? ShirtNumber,
        string? Position,
        Guid TournamentTeamId,
        string TeamName);
}
