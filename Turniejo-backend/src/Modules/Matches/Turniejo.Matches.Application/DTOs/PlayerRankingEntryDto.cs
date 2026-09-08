namespace Turniejo.Matches.Application.DTOs
{
    public sealed record PlayerRankingEntryDto(
        Guid PlayerId,
        string FirstName,
        string LastName,
        int Count);
}
