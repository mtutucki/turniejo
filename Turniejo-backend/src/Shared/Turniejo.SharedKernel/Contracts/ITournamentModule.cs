namespace Turniejo.SharedKernel.Contracts;

public interface ITournamentModule
{
    Task<TournamentDto?> GetTournamentAsync(Guid tournamentId, CancellationToken cancellationToken = default);
    Task<TournamentCategoryDto?> GetTournamentCategoryAsync(Guid categoryId, CancellationToken cancellationToken = default);
}

public record TournamentDto(Guid Id, string Name, string Status, Guid OrganizationId, int TeamLimit);
public record TournamentCategoryDto(Guid Id, Guid TournamentId, string Name, DateTime? MinBirthDate, DateTime? MaxBirthDate);
