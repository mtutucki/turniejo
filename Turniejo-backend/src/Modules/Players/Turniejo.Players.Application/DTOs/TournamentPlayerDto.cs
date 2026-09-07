namespace Turniejo.Players.Application.DTOs
{
    public sealed record TournamentPlayerDto(
        Guid Id,
        Guid TournamentTeamId,
        Guid PlayerId,
        int? ShirtNumber,
        string? Position,
        string Status);
}
