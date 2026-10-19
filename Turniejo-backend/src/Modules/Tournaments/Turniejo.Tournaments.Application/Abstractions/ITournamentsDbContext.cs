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
        Task<Tournament?> GetTournamentBySlugAsync(string slug, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<Tournament>> GetTournamentsByOrganizationIdAsync(Guid organizationId, CancellationToken cancellationToken = default);
        /// <summary>Miękko usuwa wszystkie turnieje organizacji; zwraca Id wszystkich, także usuniętych wcześniej.</summary>
        Task<IReadOnlyList<Guid>> SoftDeleteTournamentsByOrganizationIdAsync(Guid organizationId, CancellationToken cancellationToken = default);

        // TournamentCategory
        Task AddTournamentCategoryAsync(TournamentCategory category, CancellationToken cancellationToken = default);
        Task<bool> CategoryNameExistsAsync(Guid tournamentId, string name, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<TournamentCategory>> GetTournamentCategoriesByTournamentIdAsync(Guid tournamentId, CancellationToken cancellationToken = default);
        Task<TournamentCategory?> GetTournamentCategoryByIdAsync(Guid categoryId, CancellationToken cancellationToken = default);
        public Task RemoveTournamentCategoryAsync(TournamentCategory category, CancellationToken cancellationToke = default);

        // TournamentGroup
        Task AddTournamentGroupAsync(TournamentGroup group, CancellationToken cancellationToken = default);
        Task<bool> GroupNameExistsAsync(Guid categoryId, string name, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<TournamentGroup>> GetTournamentGroupsByTournamentIdAsync(Guid tournamentId, CancellationToken cancellationToken = default);
        Task<TournamentGroup?> GetTournamentGroupByIdAsync(Guid groupId, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<TournamentGroup>> GetTournamentGroupsByCategoryIdAsync(Guid categoryId, CancellationToken cancellationToken = default);
        Task RemoveTournamentGroupAsync(TournamentGroup group, CancellationToken cancellationToken = default);

        // TieBreakerRule
        Task<IReadOnlyList<TieBreakerRule>> GetTieBreakerRulesByTournamentIdAsync(Guid tournamentId, CancellationToken cancellationToken = default);
        Task ReplaceTieBreakerRulesAsync(Guid tournamentId, IReadOnlyList<TieBreakerRule> newRules, CancellationToken cancellationToken = default);

    }
}
