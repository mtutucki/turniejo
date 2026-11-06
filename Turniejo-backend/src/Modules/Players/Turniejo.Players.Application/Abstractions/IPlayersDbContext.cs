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
        Task<IReadOnlyList<Player>> GetPlayersByIdsAsync(IReadOnlyCollection<Guid> playerIds, CancellationToken cancellationToken = default);

        // TournamentPlayer
        Task AddTournamentPlayerAsync(TournamentPlayer tournamentPlayer, CancellationToken cancellationToken = default);
        Task<TournamentPlayer?> GetTournamentPlayerByIdAsync(Guid tournamentPlayerId, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<TournamentPlayer>> GetTournamentPlayersByTournamentTeamIdAsync(Guid tournamentTeamId, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<TournamentPlayer>> GetTournamentPlayersByPlayerIdAsync(Guid playerId, CancellationToken cancellationToken = default);
        Task<bool> TournamentPlayerExistsAsync(Guid tournamentTeamId, Guid playerId, CancellationToken cancellationToken = default);
        Task<bool> ShirtNumberExistsAsync(Guid tournamentTeamId, int shirtNumber, CancellationToken cancellationToken = default);
        Task RemoveTournamentPlayerAsync(TournamentPlayer tournamentPlayer, CancellationToken cancellationToken = default);
        Task RemovePlayerAsync(Player player, CancellationToken cancellationToken = default);

        // TeamPlayer (kadra drużyny)
        Task AddTeamPlayerAsync(TeamPlayer teamPlayer, CancellationToken cancellationToken = default);
        /// <summary>Śledzony — zmiany trafiają do bazy przy SaveChanges.</summary>
        Task<TeamPlayer?> GetTeamPlayerAsync(Guid teamId, Guid playerId, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<TeamPlayer>> GetTeamPlayersByTeamIdAsync(Guid teamId, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<TeamPlayer>> GetTeamPlayersByPlayerIdAsync(Guid playerId, CancellationToken cancellationToken = default);
        Task RemoveTeamPlayerAsync(TeamPlayer teamPlayer, CancellationToken cancellationToken = default);

        /// <summary>
        /// Usuwa kadry wskazanych drużyn i zawodników, którzy nie są już w żadnej kadrze ani składzie.
        /// Zwraca Id usuniętych zawodników.
        /// </summary>
        Task<IReadOnlyList<Guid>> RemoveRostersOfTeamsAsync(IReadOnlyCollection<Guid> teamIds, CancellationToken cancellationToken = default);

        // PlayerGuardian
        Task AddPlayerGuardianAsync(PlayerGuardian playerGuardian, CancellationToken cancellationToken = default);
        Task<bool> PlayerGuardianExistsAsync(Guid playerId, Guid userId, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<PlayerGuardian>> GetPlayerGuardiansByPlayerIdAsync(Guid playerId, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<PlayerGuardian>> GetPlayerGuardiansByPlayerIdsAsync(IReadOnlyCollection<Guid> playerIds, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<PlayerGuardian>> GetGuardianshipsByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
        Task RemoveGuardianshipsByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Usuwa składy wskazanych drużyn i zawodników, którzy nie grają już nigdzie indziej (opiekunowie
        /// znikają kaskadą FK). Zwraca Id usuniętych zawodników.
        /// </summary>
        Task<IReadOnlyList<Guid>> RemovePlayersOfTournamentTeamsAsync(IReadOnlyCollection<Guid> tournamentTeamIds, CancellationToken cancellationToken = default);
    }
}
