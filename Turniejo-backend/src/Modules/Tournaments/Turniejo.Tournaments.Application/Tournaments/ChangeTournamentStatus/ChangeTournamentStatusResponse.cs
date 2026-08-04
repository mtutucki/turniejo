namespace Turniejo.Tournaments.Application.Tournaments.ChangeTournamentStatus
{
    public sealed record ChangeTournamentStatusResponse(Guid TournamentId, string Slug);
}