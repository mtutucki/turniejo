using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Turniejo.SharedKernel.Outbox;

namespace Turniejo.Standings.Infrastructure.Persistence.Configurations;

public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("outbox_messages");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Type)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(x => x.Payload)
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(x => x.Error)
            .HasMaxLength(2000);

        builder.HasIndex(x => x.ProcessedAt)
            .HasFilter("\"ProcessedAt\" IS NULL")
            .HasDatabaseName("IX_outbox_messages_unprocessed");

        builder.HasIndex(x => x.Type)
            .HasDatabaseName("IX_outbox_messages_type");
    }
}
