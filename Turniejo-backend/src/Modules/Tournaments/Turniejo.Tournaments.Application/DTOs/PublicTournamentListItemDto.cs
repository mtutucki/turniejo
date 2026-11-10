namespace Turniejo.Tournaments.Application.DTOs
{
    public sealed record PublicOrganizerDto(Guid Id, string Name);

    /// <summary>Karta turnieju na liście publicznej. Bez opisu i ustawień, te są na stronie portalu.</summary>
    public sealed record PublicTournamentListItemDto(
        string Name,
        string Slug,
        string Status,
        DateTimeOffset StartAt,
        DateTimeOffset EndAt,
        PublicOrganizerDto Organizer,
        IReadOnlyList<string> Categories,
        int TeamCount,
        int LiveMatchCount);
}
