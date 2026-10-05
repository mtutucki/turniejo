namespace Turniejo.Tournaments.Application.Tournaments.JoinTournament
{
    public sealed record JoinTournamentResponse(Guid TournamentId, string TournamentName, string Slug, string Role, bool IsPublic);
}
