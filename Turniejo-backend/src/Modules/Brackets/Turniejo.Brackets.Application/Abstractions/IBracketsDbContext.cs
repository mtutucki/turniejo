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

        // BracketRound
        Task AddBracketRoundAsync(BracketRound round, CancellationToken cancellationToken = default);
        Task<BracketRound?> GetBracketRoundByIdAsync(Guid roundId, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<BracketRound>> GetBracketRoundsByBracketIdAsync(Guid bracketId, CancellationToken cancellationToken = default);

        // BracketNode
        Task AddBracketNodeAsync(BracketNode node, CancellationToken cancellationToken = default);
        Task<BracketNode?> GetBracketNodeByIdAsync(Guid nodeId, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<BracketNode>> GetBracketNodesByRoundIdsAsync(IReadOnlyCollection<Guid> roundIds, CancellationToken cancellationToken = default);
        Task<BracketNode?> GetBracketNodeByMatchIdAsync(Guid matchId, CancellationToken cancellationToken = default);
    }
}
