namespace Turniejo.Tournaments.Application.DTOs
{
    public sealed record TournamentGroupDto(
        Guid Id,
        Guid TournamentId,
        Guid CategoryId,
        string Name,
        int Order);
}
