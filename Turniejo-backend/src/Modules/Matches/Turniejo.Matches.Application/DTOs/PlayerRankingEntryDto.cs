namespace Turniejo.Matches.Application.DTOs
{
    public sealed record PlayerRankingEntryDto(
        Guid PlayerId,
        string FirstName,
        string LastName,
        int Count,
        // Drużyna zawodnika w turnieju — portal pokazuje jej nazwę (o ile ShowTeams).
        Guid? TournamentTeamId = null);
}
