using Microsoft.EntityFrameworkCore;
using Turniejo.Brackets.Application.Abstractions;
using Turniejo.Brackets.Domain.Entities;
using Turniejo.SharedKernel.Outbox;

namespace Turniejo.Brackets.Infrastructure.Persistence;

public sealed class BracketsDbContext(DbContextOptions<BracketsDbContext> options)
    : DbContext(options), IBracketsDbContext
{
    public DbSet<Bracket> Brackets => Set<Bracket>();
    public DbSet<BracketRound> BracketRounds => Set<BracketRound>();
    public DbSet<BracketNode> BracketNodes => Set<BracketNode>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    public async Task AddOutboxMessageAsync(OutboxMessage message, CancellationToken cancellationToken = default) =>
        await OutboxMessages.AddAsync(message, cancellationToken);

    public new async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        await base.SaveChangesAsync(cancellationToken);

    public async Task AddBracketAsync(Bracket bracket, CancellationToken cancellationToken = default) =>
        await Brackets.AddAsync(bracket, cancellationToken);

    public async Task<Bracket?> GetBracketByIdAsync(Guid bracketId, CancellationToken cancellationToken = default) =>
        await Brackets.FirstOrDefaultAsync(b => b.Id == bracketId, cancellationToken);

    public async Task<Bracket?> GetBracketByTournamentAndCategoryAsync(Guid tournamentId, Guid categoryId, CancellationToken cancellationToken = default) =>
        await Brackets.FirstOrDefaultAsync(b => b.TournamentId == tournamentId && b.CategoryId == categoryId, cancellationToken);

    public async Task<IReadOnlyList<Bracket>> GetBracketsByTournamentIdAsync(Guid tournamentId, CancellationToken cancellationToken = default) =>
        await Brackets
            .AsNoTracking()
            .Where(b => b.TournamentId == tournamentId)
            .ToListAsync(cancellationToken);

    public Task RemoveBracketAsync(Bracket bracket, CancellationToken cancellationToken = default)
    {
        Brackets.Remove(bracket);
        return Task.CompletedTask;
    }

    public async Task AddBracketRoundAsync(BracketRound round, CancellationToken cancellationToken = default) =>
        await BracketRounds.AddAsync(round, cancellationToken);

    public async Task<BracketRound?> GetBracketRoundByIdAsync(Guid roundId, CancellationToken cancellationToken = default) =>
        await BracketRounds.FirstOrDefaultAsync(r => r.Id == roundId, cancellationToken);

    public async Task<IReadOnlyList<BracketRound>> GetBracketRoundsByBracketIdAsync(Guid bracketId, CancellationToken cancellationToken = default) =>
        await BracketRounds
            .AsNoTracking()
            .Where(r => r.BracketId == bracketId)
            .ToListAsync(cancellationToken);

    public async Task AddBracketNodeAsync(BracketNode node, CancellationToken cancellationToken = default) =>
        await BracketNodes.AddAsync(node, cancellationToken);

    public async Task<BracketNode?> GetBracketNodeByIdAsync(Guid nodeId, CancellationToken cancellationToken = default) =>
        await BracketNodes.FirstOrDefaultAsync(n => n.Id == nodeId, cancellationToken);

    public async Task<IReadOnlyList<BracketNode>> GetBracketNodesByRoundIdsAsync(IReadOnlyCollection<Guid> roundIds, CancellationToken cancellationToken = default) =>
        await BracketNodes
            .AsNoTracking()
            .Where(n => roundIds.Contains(n.BracketRoundId))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<BracketNode>> GetTrackedBracketNodesByIdsAsync(IReadOnlyCollection<Guid> nodeIds, CancellationToken cancellationToken = default) =>
        await BracketNodes
            .Where(n => nodeIds.Contains(n.Id))
            .ToListAsync(cancellationToken);

    public async Task<BracketNode?> GetBracketNodeByMatchIdAsync(Guid matchId, CancellationToken cancellationToken = default) =>
        await BracketNodes.FirstOrDefaultAsync(n => n.MatchId == matchId, cancellationToken);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("brackets");

        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            if (entity.FindPrimaryKey()?.Properties.Any(p => p.ClrType == typeof(Guid)) == true)
            {
                entity.FindPrimaryKey()!.Properties
                    .Where(p => p.ClrType == typeof(Guid))
                    .ToList()
                    .ForEach(p => p.ValueGenerated = Microsoft.EntityFrameworkCore.Metadata.ValueGenerated.Never);
            }
        }

        foreach (var relationship in modelBuilder.Model.GetEntityTypes().SelectMany(e => e.GetForeignKeys()))
        {
            relationship.DeleteBehavior = DeleteBehavior.Restrict;
        }

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BracketsDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
