namespace Turniejo.Teams.Application.DTOs
{
    public sealed record TournamentTeamDto(
        Guid Id,
        Guid TournamentId,
        Guid TeamId,
        Guid CategoryId,
        Guid? GroupId,
        string RegistrationStatus,
        string PaymentStatus,
        int? SeedNumber,
        // Dane globalnej drużyny — bez nich UI musiałby dociągać każdą drużynę osobnym zapytaniem.
        string TeamName,
        string? TeamShortName,
        string? TeamLogoUrl);
}
