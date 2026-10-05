namespace Turniejo.SharedKernel.Contracts;

public interface IStandingModule
{
    Task<IReadOnlyList<GroupStandingDto>> GetGroupStandingsAsync(Guid groupId, CancellationToken cancellationToken = default);
}

// Statystyki na końcu z domyślnymi wartościami — istniejące wywołania konstruktora bez zmian.
public record GroupStandingDto(
    Guid TournamentTeamId, int Position, int Points,
    int MatchesPlayed = 0, int Wins = 0, int Draws = 0, int Losses = 0, int GoalsFor = 0, int GoalsAgainst = 0);
