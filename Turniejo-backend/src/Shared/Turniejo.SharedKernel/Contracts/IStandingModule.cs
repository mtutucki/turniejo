namespace Turniejo.SharedKernel.Contracts;

public interface IStandingModule
{
    Task<IReadOnlyList<GroupStandingDto>> GetGroupStandingsAsync(Guid groupId, CancellationToken cancellationToken = default);
}

public record GroupStandingDto(Guid TournamentTeamId, int Position, int Points);
