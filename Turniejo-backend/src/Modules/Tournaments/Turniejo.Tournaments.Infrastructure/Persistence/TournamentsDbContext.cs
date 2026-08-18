using Microsoft.EntityFrameworkCore;
using Turniejo.SharedKernel.Outbox;
using Turniejo.Tournaments.Application.Abstractions;
using Turniejo.Tournaments.Domain.Entities;

namespace Turniejo.Tournaments.Infrastructure.Persistence;

public sealed class TournamentsDbContext(DbContextOptions<TournamentsDbContext> options)
    : DbContext(options), ITournamentsDbContext
{
    public DbSet<Tournament> Tournaments => Set<Tournament>();
    public DbSet<TournamentCategory> TournamentCategories => Set<TournamentCategory>();
    public DbSet<TournamentGroup> TournamentGroups => Set<TournamentGroup>();
    public DbSet<TieBreakerRule> TieBreakerRules => Set<TieBreakerRule>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    public async Task AddOutboxMessageAsync(OutboxMessage message, CancellationToken cancellationToken = default)
    {
        await OutboxMessages.AddAsync(message, cancellationToken);
    }

    public async Task AddTournamentAsync(Tournament tournament, CancellationToken cancellationToken)
    {
        await Tournaments.AddAsync(tournament, cancellationToken);
    }

    public async Task<bool> TournamentSlugExistsAsync(string slug, CancellationToken cancellationToken)
    {
        return await Tournaments.AnyAsync(t => t.Slug == slug);
    }

    public async Task<Tournament?> GetTournamentByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        return await Tournaments.FirstOrDefaultAsync(t => t.RefereeCode == code || t.CoachCode == code || t.ViewerCode == code, cancellationToken);
    }

    public new async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
    await base.SaveChangesAsync(cancellationToken);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("tournaments");

        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            if (entity.FindPrimaryKey()?.Properties
                .Any(p => p.ClrType == typeof(Guid)) == true)
            {
                entity.FindPrimaryKey()!.Properties
                    .Where(p => p.ClrType == typeof(Guid))
                    .ToList()
                    .ForEach(p => p.ValueGenerated =
                        Microsoft.EntityFrameworkCore.Metadata.ValueGenerated.Never);
            }
        }

        foreach (var relationship in modelBuilder.Model.GetEntityTypes().SelectMany(e => e.GetForeignKeys()))
        {
            relationship.DeleteBehavior = DeleteBehavior.Restrict;
        }

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TournamentsDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }

    public async Task<Tournament?> GetTournamentByIdAsync(Guid tournamentId, CancellationToken cancellationToken = default)
    {
        return await Tournaments.FirstOrDefaultAsync(t => t.Id == tournamentId, cancellationToken);
    }

    public async Task<IReadOnlyList<Tournament>> GetTournamentsByOrganizationIdAsync(Guid organizationId, CancellationToken cancellationToken = default) =>
    await Tournaments
        .AsNoTracking()
        .Where(t => t.OrganizationId == organizationId)
        .OrderByDescending(t => t.CreatedAt)
        .ToListAsync(cancellationToken);

    public async Task AddTournamentCategoryAsync(TournamentCategory category, CancellationToken cancellationToken = default) =>
    await TournamentCategories.AddAsync(category, cancellationToken);

    public async Task<bool> CategoryNameExistsAsync(Guid tournamentId, string name, CancellationToken cancellationToken = default) =>
        await TournamentCategories.AnyAsync(c => c.TournamentId == tournamentId && c.Name == name, cancellationToken);

    public async Task<IReadOnlyList<TournamentCategory>> GetTournamentCategoriesByTournamentIdAsync(Guid tournamentId, CancellationToken cancellationToken = default) =>
        await TournamentCategories
            .AsNoTracking()
            .Where(c => c.TournamentId == tournamentId)
            .OrderBy(c => c.Order)
            .ToListAsync(cancellationToken);

    public async Task AddTournamentGroupAsync(TournamentGroup group, CancellationToken cancellationToken = default) =>
        await TournamentGroups.AddAsync(group, cancellationToken);

    public async Task<bool> GroupNameExistsAsync(Guid categoryId, string name, CancellationToken cancellationToken = default) =>
        await TournamentGroups.AnyAsync(g => g.CategoryId == categoryId && g.Name == name, cancellationToken);

    public async Task<IReadOnlyList<TournamentGroup>> GetTournamentGroupsByTournamentIdAsync(Guid tournamentId, CancellationToken cancellationToken = default) =>
        await TournamentGroups
            .AsNoTracking()
            .Where(g => g.TournamentId == tournamentId)
            .OrderBy(g => g.Order)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<TieBreakerRule>> GetTieBreakerRulesByTournamentIdAsync(Guid tournamentId, CancellationToken cancellationToken = default) =>
        await TieBreakerRules
            .AsNoTracking()
            .Where(r => r.TournamentId == tournamentId)
            .OrderBy(r => r.Order)
            .ToListAsync(cancellationToken);

    public async Task ReplaceTieBreakerRulesAsync(Guid tournamentId, IReadOnlyList<TieBreakerRule> newRules, CancellationToken cancellationToken = default)
    {
        var existing = await TieBreakerRules
            .Where(r => r.TournamentId == tournamentId)
            .ToListAsync(cancellationToken);

        TieBreakerRules.RemoveRange(existing);
        await TieBreakerRules.AddRangeAsync(newRules, cancellationToken);
    }
}