namespace Turniejo.SharedKernel.Contracts;

public interface ITeamModule
{
    Task<TeamDto?> GetTeamAsync(Guid teamId, CancellationToken cancellationToken = default);
}

public record TeamDto(Guid Id, string Name, string? ShortName);