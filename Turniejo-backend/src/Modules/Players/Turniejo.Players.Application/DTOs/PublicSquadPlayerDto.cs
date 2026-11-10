namespace Turniejo.Players.Application.DTOs
{
    /// <summary>Zawodnik w składzie drużyny na portalu. PlayerId łączy go z rankingami strzelców i asyst.</summary>
    public sealed record PublicSquadPlayerDto(
        Guid TournamentPlayerId,
        Guid PlayerId,
        string FirstName,
        string LastName,
        int? ShirtNumber,
        string? Position);
}
