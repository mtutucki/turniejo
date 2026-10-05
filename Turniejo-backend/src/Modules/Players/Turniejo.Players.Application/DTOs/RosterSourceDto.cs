namespace Turniejo.Players.Application.DTOs
{
    // Poprzednie zgłoszenie tej samej drużyny razem ze składem, który można skopiować do bieżącego.
    public sealed record RosterSourceDto(
        Guid TournamentTeamId,
        Guid TournamentId,
        string TournamentName,
        DateTimeOffset? TournamentStartAt,
        string? CategoryName,
        IReadOnlyList<RosterSourcePlayerDto> Players);

    public sealed record RosterSourcePlayerDto(
        Guid PlayerId,
        string FirstName,
        string LastName,
        DateTime BirthDate,
        int? ShirtNumber,
        string? Position,
        // Liczone względem drużyny docelowej: zawodnik już w jej składzie / poza rocznikiem jej kategorii.
        bool AlreadyInSquad,
        // null | "TooOld" | "TooYoung"
        string? Ineligibility);
}
