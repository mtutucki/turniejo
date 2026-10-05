using Microsoft.EntityFrameworkCore;
using Npgsql;
using Turniejo.Organizations.Application.Abstractions;
using Turniejo.Organizations.Domain.Entities;
using Turniejo.SharedKernel.Exceptions;
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

    public async Task<Organization?> GetOrganizationByOwnerUserIdAsync(
        Guid ownerUserId,
        CancellationToken cancellationToken = default) =>
        await Organizations.FirstOrDefaultAsync(
            x => x.OwnerUserId == ownerUserId,
            cancellationToken);

    public new async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await base.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505", ConstraintName: "IX_organizations_owner" })
        {
            // Aplikacyjny check-then-insert (OrganizationExistsForOwnerAsync) to nie wystarczy przy
            // równoległych żądaniach — to jest ostatnia linia obrony, więc zamieniamy surowy błąd bazy
            // na czytelny DomainException (422), a nie 500.
            throw new DomainException("You already have an organization.");
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505", ConstraintName: "IX_organizations_slug" })
        {
            // Organization.Create nie ma żadnego check-then-insert dla sluga (w przeciwieństwie do
            // ownera powyżej) — poleganie wyłącznie na indeksie unikalnym też kończyło się surowym 500
            // zamiast czytelnego 422. Slug ma losowy sufiks (SlugGenerator), więc kolizja jest rzadka.
            throw new DomainException("An organization with this slug already exists — please try again.");
        }
    }

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