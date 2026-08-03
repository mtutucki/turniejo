using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Turniejo.Users.Domain.Entities;

namespace Turniejo.Users.Infrastructure.Persistence.Configurations;

public sealed class UserRoleAssignmentConfiguration
    : IEntityTypeConfiguration<UserRoleAssignment>
{
    public void Configure(EntityTypeBuilder<UserRoleAssignment> builder)
    {
        builder.ToTable("user_role_assignments");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Role)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.ScopeType)
            .HasConversion<int>()
            .IsRequired();

        builder.HasIndex(x => new { x.UserId, x.Role, x.ScopeType, x.ScopeId })
            .HasDatabaseName("IX_user_role_assignments_composite");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}