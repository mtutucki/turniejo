namespace Turniejo.Teams.Application.DTOs
{
    public sealed record TeamPlayerStatsDto(
        Guid TournamentPlayerId,
        Guid PlayerId,
        string FirstName,
        string LastName,
        int Goals,
        int Assists,
        int YellowCards,
        int RedCards);
}
