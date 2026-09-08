using Turniejo.SharedKernel.Outbox;
using Turniejo.Standings.Domain.Entities;

namespace Turniejo.Standings.Application.Abstractions
{
    public interface IStandingsDbContext
    {
        Task AddOutboxMessageAsync(OutboxMessage message, CancellationToken cancellationToken = default);
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

        Task AddStandingAsync(Standing standing, CancellationToken cancellationToken = default);
        Task<Standing?> GetStandingAsync(Guid groupId, Guid tournamentTeamId, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<Standing>> GetStandingsByGroupIdAsync(Guid groupId, CancellationToken cancellationToken = default);
        Task RemoveStandingAsync(Standing standing, CancellationToken cancellationToken = default);
    }
}
