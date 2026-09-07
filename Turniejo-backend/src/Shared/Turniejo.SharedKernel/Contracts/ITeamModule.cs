namespace Turniejo.SharedKernel.Contracts;

public interface ITeamModule
{
    Task<TeamDto?> GetTeamAsync(Guid teamId, CancellationToken cancellationToken = default);
    Task<TournamentTeamDto?> GetTournamentTeamAsync(Guid tournamentTeamId, CancellationToken cancellationToken = default);
}

public record TeamDto(Guid Id, string Name, string? ShortName);
public record TournamentTeamDto(Guid Id, Guid TournamentId, Guid TeamId, Guid CategoryId, Guid? GroupId);
