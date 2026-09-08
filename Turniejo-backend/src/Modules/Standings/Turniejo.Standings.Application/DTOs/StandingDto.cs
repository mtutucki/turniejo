namespace Turniejo.Standings.Application.DTOs
{
    public sealed record StandingDto(
        Guid Id,
        Guid TournamentId,
        Guid CategoryId,
        Guid GroupId,
        Guid TournamentTeamId,
        int MatchesPlayed,
        int Wins,
        int Draws,
        int Losses,
        int GoalsFor,
        int GoalsAgainst,
        int GoalDifference,
        int Points,
        int Position);
}
