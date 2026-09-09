namespace Turniejo.SharedKernel.Contracts;

public interface ITeamModule
{
    Task<TeamDto?> GetTeamAsync(Guid teamId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TeamDto>> GetTeamsAsync(IReadOnlyCollection<Guid> teamIds, CancellationToken cancellationToken = default);
    Task<TournamentTeamDto?> GetTournamentTeamAsync(Guid tournamentTeamId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TournamentTeamDto>> GetTournamentTeamsByTournamentIdAsync(Guid tournamentId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Guid>> GetTeamStaffUserIdsAsync(Guid tournamentTeamId, CancellationToken cancellationToken = default);

    // Cross-module istnienie-check dla blokad kasowania (np. kategorii turnieju) — TournamentTeam.CategoryId
    // nie ma FK, więc bez tego usunięcie kategorii cicho osierocało zarejestrowane drużyny.
    Task<bool> AnyTeamsInCategoryAsync(Guid categoryId, CancellationToken cancellationToken = default);
}

public record TeamDto(Guid Id, string Name, string? ShortName);
public record TournamentTeamDto(Guid Id, Guid TournamentId, Guid TeamId, Guid CategoryId, Guid? GroupId, string RegistrationStatus);
