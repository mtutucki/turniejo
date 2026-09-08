using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Turniejo.Players.Domain.Entities;

namespace Turniejo.Players.Infrastructure.Persistence.Configurations
{
    public sealed class PlayerGuardianConfiguration : IEntityTypeConfiguration<PlayerGuardian>
    {
        public void Configure(EntityTypeBuilder<PlayerGuardian> builder)
        {
            builder.ToTable("player_guardians");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.RelationshipType)
                .HasConversion<int>();

            builder.HasIndex(x => new { x.PlayerId, x.UserId })
                .IsUnique();

            builder.HasOne<Player>()
                .WithMany()
                .HasForeignKey(x => x.PlayerId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
