using Microsoft.EntityFrameworkCore;
using Turniejo.Organizations.Application.Abstractions;
using Turniejo.Organizations.Domain.Entities;
using Turniejo.SharedKernel.Outbox;

namespace Turniejo.Organizations.Infrastructure.Persistence;

public sealed class OrganizationsDbContext(DbContextOptions<OrganizationsDbContext> options)
    : DbContext(options), IOrganizationsDbContext
{
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    public async Task AddOrganizationAsync(
        Organization organization,
        CancellationToken cancellationToken = default) =>
        await Organizations.AddAsync(organization, cancellationToken);

    public async Task AddOutboxMessageAsync(
        OutboxMessage message,
        CancellationToken cancellationToken = default) =>
        await OutboxMessages.AddAsync(message, cancellationToken);

    public async Task<bool> OrganizationExistsForOwnerAsync(
        Guid ownerUserId,
        CancellationToken cancellationToken = default) =>
        await Organizations.AnyAsync(
            x => x.OwnerUserId == ownerUserId,
            cancellationToken);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("organizations");

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

        foreach (var relationship in modelBuilder.Model.GetEntityTypes()
            .SelectMany(e => e.GetForeignKeys()))
        {
            relationship.DeleteBehavior = DeleteBehavior.Restrict;
        }

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(OrganizationsDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}