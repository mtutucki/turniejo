using Microsoft.EntityFrameworkCore;
using Turniejo.SharedKernel.Enums;
using Turniejo.SharedKernel.Outbox;
using Turniejo.Users.Application.Abstractions;
using Turniejo.Users.Domain.Entities;

namespace Turniejo.Users.Infrastructure.Persistence;

public sealed class UsersDbContext(DbContextOptions<UsersDbContext> options)
    : DbContext(options), IUsersDbContext
{
    public DbSet<User> Users => Set<User>();
    public DbSet<UserRoleAssignment> UserRoleAssignments => Set<UserRoleAssignment>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    public async Task AddUserAsync(User user, CancellationToken ct) =>
        await Users.AddAsync(user, ct);

    public async Task AddOutboxMessageAsync(OutboxMessage message, CancellationToken ct) =>
        await OutboxMessages.AddAsync(message, ct);

    public async Task<User?> GetUserByEmailAsync(
    string email,
    CancellationToken cancellationToken = default) => await Users.FirstOrDefaultAsync(u => u.Email == email, cancellationToken);

    public async Task AddUserRoleAssignmentAsync(
    UserRoleAssignment assignment,
    CancellationToken cancellationToken = default) =>
    await UserRoleAssignments.AddAsync(assignment, cancellationToken);

    public async Task<bool> UserRoleAssignmentExistsAsync(
        Guid userId, UserRole role, RoleScopeType scopeType, Guid? scopeId,
        CancellationToken cancellationToken = default) =>
        await UserRoleAssignments.AnyAsync(
            x => x.UserId == userId &&
                 x.Role == role &&
                 x.ScopeType == scopeType &&
                 x.ScopeId == scopeId,
            cancellationToken);

    public async Task<IReadOnlyList<UserRoleAssignment>> GetUserRolesAsync(
        Guid userId,
        CancellationToken cancellationToken = default) =>
        await UserRoleAssignments
            .Where(x => x.UserId == userId)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<UserRoleAssignment>> GetUserRoleAssignmentsByScopeAsync(
        RoleScopeType scopeType,
        Guid scopeId,
        CancellationToken cancellationToken = default) =>
        await UserRoleAssignments
            .Where(x => x.ScopeType == scopeType && x.ScopeId == scopeId)
            .ToListAsync(cancellationToken);

    public async Task RemoveUserRoleAssignmentAsync(
        Guid userId, UserRole role, RoleScopeType scopeType, Guid? scopeId,
        CancellationToken cancellationToken = default)
    {
        var assignment = await UserRoleAssignments
            .FirstOrDefaultAsync(
                x => x.UserId == userId &&
                     x.Role == role &&
                     x.ScopeType == scopeType &&
                     x.ScopeId == scopeId,
                cancellationToken);

        if (assignment is not null)
            UserRoleAssignments.Remove(assignment);
    }

    public void RemoveUserRoleAssignments(IEnumerable<UserRoleAssignment> assignments) =>
        UserRoleAssignments.RemoveRange(assignments);

    public async Task<User?> GetUserByIdAsync(Guid userId, CancellationToken cancellationToken = default) => await Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("users");

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

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(UsersDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}