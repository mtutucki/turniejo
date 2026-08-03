using Microsoft.EntityFrameworkCore;
using Turniejo.SharedKernel.Outbox;
using Turniejo.Tournaments.Application.Abstractions;
using Turniejo.Tournaments.Domain.Entities;

namespace Turniejo.Tournaments.Infrastructure.Persistence;

public sealed class TournamentsDbContext(DbContextOptions<TournamentsDbContext> options)
    : DbContext(options), ITournamentsDbContext
{
    public DbSet<Tournament> Tournaments => Set<Tournament>();
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
}