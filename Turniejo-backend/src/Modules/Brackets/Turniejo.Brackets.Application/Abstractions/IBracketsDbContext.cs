using Turniejo.Brackets.Domain.Entities;
using Turniejo.SharedKernel.Outbox;

namespace Turniejo.Brackets.Application.Abstractions
{
    public interface IBracketsDbContext
    {
        Task AddOutboxMessageAsync(OutboxMessage message, CancellationToken cancellationToken = default);
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

        // Bracket
        Task AddBracketAsync(Bracket bracket, CancellationToken cancellationToken = default);
        Task<Bracket?> GetBracketByIdAsync(Guid bracketId, CancellationToken cancellationToken = default);
        Task<Bracket?> GetBracketByTournamentAndCategoryAsync(Guid tournamentId, Guid categoryId, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<Bracket>> GetBracketsByTournamentIdAsync(Guid tournamentId, CancellationToken cancellationToken = default);
        // Rundy i węzły znikają kaskadowo w bazie (FK ON DELETE CASCADE).
        Task RemoveBracketAsync(Bracket bracket, CancellationToken cancellationToken = default);

        // BracketRound
        Task AddBracketRoundAsync(BracketRound round, CancellationToken cancellationToken = default);
        Task<BracketRound?> GetBracketRoundByIdAsync(Guid roundId, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<BracketRound>> GetBracketRoundsByBracketIdAsync(Guid bracketId, CancellationToken cancellationToken = default);

        // BracketNode
        Task AddBracketNodeAsync(BracketNode node, CancellationToken cancellationToken = default);
        Task<BracketNode?> GetBracketNodeByIdAsync(Guid nodeId, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<BracketNode>> GetBracketNodesByRoundIdsAsync(IReadOnlyCollection<Guid> roundIds, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<BracketNode>> GetTrackedBracketNodesByIdsAsync(IReadOnlyCollection<Guid> nodeIds, CancellationToken cancellationToken = default);
        Task<BracketNode?> GetBracketNodeByMatchIdAsync(Guid matchId, CancellationToken cancellationToken = default);

        // SecondStageSeat (format „Dwie fazy grupowe”)
        Task<IReadOnlyList<SecondStageSeat>> GetSecondStageSeatsAsync(Guid tournamentId, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<SecondStageSeat>> GetTrackedSecondStageSeatsAsync(Guid tournamentId, CancellationToken cancellationToken = default);
        /// <summary>Miejsca w grupie <paramref name="groupId"/> albo z niej (jako grupy fazy 1).</summary>
        Task<IReadOnlyList<SecondStageSeat>> GetTrackedSecondStageSeatsByGroupAsync(Guid groupId, CancellationToken cancellationToken = default);
        Task AddSecondStageSeatsAsync(IEnumerable<SecondStageSeat> seats, CancellationToken cancellationToken = default);
        void RemoveSecondStageSeats(IEnumerable<SecondStageSeat> seats);

        // SecondStageFixture
        Task<IReadOnlyList<SecondStageFixture>> GetSecondStageFixturesAsync(Guid tournamentId, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<SecondStageFixture>> GetTrackedSecondStageFixturesAsync(Guid tournamentId, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<SecondStageFixture>> GetTrackedSecondStageFixturesByGroupIdsAsync(IReadOnlyCollection<Guid> groupIds, CancellationToken cancellationToken = default);
        Task<SecondStageFixture?> GetSecondStageFixtureByMatchIdAsync(Guid matchId, CancellationToken cancellationToken = default);
        Task AddSecondStageFixturesAsync(IEnumerable<SecondStageFixture> fixtures, CancellationToken cancellationToken = default);
        void RemoveSecondStageFixtures(IEnumerable<SecondStageFixture> fixtures);
    }
}
