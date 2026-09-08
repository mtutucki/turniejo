namespace Turniejo.SharedKernel.Contracts;

public interface ITeamModule
{
    Task<TeamDto?> GetTeamAsync(Guid teamId, CancellationToken cancellationToken = default);
    Task<TournamentTeamDto?> GetTournamentTeamAsync(Guid tournamentTeamId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TournamentTeamDto>> GetTournamentTeamsByTournamentIdAsync(Guid tournamentId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Guid>> GetTeamStaffUserIdsAsync(Guid tournamentTeamId, CancellationToken cancellationToken = default);
}

public record TeamDto(Guid Id, string Name, string? ShortName);
public record TournamentTeamDto(Guid Id, Guid TournamentId, Guid TeamId, Guid CategoryId, Guid? GroupId, string RegistrationStatus);
