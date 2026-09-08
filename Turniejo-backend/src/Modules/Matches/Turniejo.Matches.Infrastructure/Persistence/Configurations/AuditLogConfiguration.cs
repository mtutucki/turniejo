using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Turniejo.Matches.Domain.Entities;

namespace Turniejo.Matches.Infrastructure.Persistence.Configurations
{
    public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
    {
        public void Configure(EntityTypeBuilder<AuditLog> builder)
        {
            builder.ToTable("audit_logs");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.EntityType)
                .HasMaxLength(100)
                .IsRequired();

            builder.Property(x => x.Action)
                .HasMaxLength(100)
                .IsRequired();

            builder.Property(x => x.OldValue)
                .HasColumnType("jsonb");

            builder.Property(x => x.NewValue)
                .HasColumnType("jsonb");

            builder.HasIndex(x => new { x.EntityType, x.EntityId });
        }
    }
}
