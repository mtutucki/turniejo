namespace Turniejo.Tournaments.Application.DTOs
{
    public sealed record TournamentCategoryDto(
        Guid Id,
        Guid TournamentId,
        string Name,
        DateTime? MinBirthDate,
        DateTime? MaxBirthDate,
        int Order);
}
