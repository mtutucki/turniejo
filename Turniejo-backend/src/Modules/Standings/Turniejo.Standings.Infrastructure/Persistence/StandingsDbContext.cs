using Microsoft.EntityFrameworkCore;
using Turniejo.SharedKernel.Outbox;
using Turniejo.Standings.Application.Abstractions;
using Turniejo.Standings.Domain.Entities;

namespace Turniejo.Standings.Infrastructure.Persistence;

public sealed class StandingsDbContext(DbContextOptions<StandingsDbContext> options)
    : DbContext(options), IStandingsDbContext
{
    public DbSet<Standing> Standings => Set<Standing>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    public async Task AddOutboxMessageAsync(OutboxMessage message, CancellationToken cancellationToken = default) =>
        await OutboxMessages.AddAsync(message, cancellationToken);

    public new async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        await base.SaveChangesAsync(cancellationToken);

    public async Task AddStandingAsync(Standing standing, CancellationToken cancellationToken = default) =>
        await Standings.AddAsync(standing, cancellationToken);

    public async Task<Standing?> GetStandingAsync(Guid groupId, Guid tournamentTeamId, CancellationToken cancellationToken = default) =>
        await Standings.FirstOrDefaultAsync(s => s.GroupId == groupId && s.TournamentTeamId == tournamentTeamId, cancellationToken);

    public async Task<IReadOnlyList<Standing>> GetStandingsByGroupIdAsync(Guid groupId, CancellationToken cancellationToken = default) =>
        await Standings
            .AsNoTracking()
            .Where(s => s.GroupId == groupId)
            .OrderBy(s => s.Position)
            .ToListAsync(cancellationToken);

    public Task RemoveStandingAsync(Standing standing, CancellationToken cancellationToken = default)
    {
        Standings.Remove(standing);
        return Task.CompletedTask;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("standings");

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

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(StandingsDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
