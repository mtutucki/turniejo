namespace Turniejo.Players.Application.TournamentPlayers.ImportSquad
{
    public sealed record ImportSquadResponse(IReadOnlyList<Guid> TournamentPlayerIds);
}
