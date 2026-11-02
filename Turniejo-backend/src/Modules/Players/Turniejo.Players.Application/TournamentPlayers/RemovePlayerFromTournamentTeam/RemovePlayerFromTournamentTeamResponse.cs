namespace Turniejo.Players.Application.TournamentPlayers.RemovePlayerFromTournamentTeam
{
    /// <param name="Deleted">true: wpis usunięty ze składu; false: zostawiony ze statusem Removed (ma wydarzenia w meczach).</param>
    public sealed record RemovePlayerFromTournamentTeamResponse(Guid TournamentPlayerId, bool Deleted);
}
