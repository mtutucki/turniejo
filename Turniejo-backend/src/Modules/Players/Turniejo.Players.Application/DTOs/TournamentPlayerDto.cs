namespace Turniejo.Players.Application.DTOs
{
    public sealed record TournamentPlayerDto(
        Guid Id,
        Guid TournamentTeamId,
        Guid PlayerId,
        int? ShirtNumber,
        string? Position,
        string Status,
        // Dane globalnego profilu zawodnika — bez nich skład wymagałby zapytania per zawodnik.
        string FirstName,
        string LastName,
        DateTime? BirthDate,
        string? PhotoUrl);
}
