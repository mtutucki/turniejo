namespace Turniejo.Players.Application.DTOs
{
    public sealed record PlayerBasicStatsDto(Guid PlayerId, int Goals, int Assists, int YellowCards, int RedCards);
}
