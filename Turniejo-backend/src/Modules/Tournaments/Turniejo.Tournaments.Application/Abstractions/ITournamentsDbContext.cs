using Turniejo.SharedKernel.Outbox;
using Turniejo.Tournaments.Domain.Entities;

namespace Turniejo.Tournaments.Application.Abstractions
{
    public interface ITournamentsDbContext
    {
        Task AddTournamentAsync(Tournament tournament, CancellationToken cancellationToken);
        Task AddOutboxMessageAsync(OutboxMessage message, CancellationToken cancellationToken = default);
        Task<bool> TournamentSlugExistsAsync(string slug, CancellationToken cancellationToken);
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
        Task<Tournament?> GetTournamentByCodeAsync(string code, CancellationToken cancellationToken = default);
        Task<Tournament?> GetTournamentByIdAsync(Guid tournamentId, CancellationToken cancellationToken = default);
    }
}
