namespace Turniejo.SharedKernel.Contracts;

public interface ITournamentModule
{
    Task<TournamentDto?> GetTournamentAsync(Guid tournamentId, CancellationToken cancellationToken = default);
}

public record TournamentDto(Guid Id, string Name, string Status, Guid OrganizationId);