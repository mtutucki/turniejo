namespace Turniejo.Players.Application.DTOs
{
    public sealed record MyChildTeamDto(
        Guid TournamentPlayerId,
        Guid TournamentTeamId,
        Guid TournamentId,
        string TournamentName,
        Guid TeamId,
        string TeamName,
        Guid? GroupId,
        int? ShirtNumber,
        string? Position);
}
