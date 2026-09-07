using Turniejo.Players.Domain.Entities;
using Turniejo.SharedKernel.Outbox;

namespace Turniejo.Players.Application.Abstractions
{
    public interface IPlayersDbContext
    {
        Task AddOutboxMessageAsync(OutboxMessage message, CancellationToken cancellationToken = default);
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

        // Player
        Task AddPlayerAsync(Player player, CancellationToken cancellationToken = default);
        Task<Player?> GetPlayerByIdAsync(Guid playerId, CancellationToken cancellationToken = default);

        // TournamentPlayer
        Task AddTournamentPlayerAsync(TournamentPlayer tournamentPlayer, CancellationToken cancellationToken = default);
        Task<TournamentPlayer?> GetTournamentPlayerByIdAsync(Guid tournamentPlayerId, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<TournamentPlayer>> GetTournamentPlayersByTournamentTeamIdAsync(Guid tournamentTeamId, CancellationToken cancellationToken = default);
        Task<bool> TournamentPlayerExistsAsync(Guid tournamentTeamId, Guid playerId, CancellationToken cancellationToken = default);
        Task<bool> ShirtNumberExistsAsync(Guid tournamentTeamId, int shirtNumber, CancellationToken cancellationToken = default);
    }
}
