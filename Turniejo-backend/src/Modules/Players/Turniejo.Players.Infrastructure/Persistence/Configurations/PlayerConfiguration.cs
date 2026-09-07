using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Turniejo.Players.Domain.Entities;

namespace Turniejo.Players.Infrastructure.Persistence.Configurations
{
    public sealed class PlayerConfiguration : IEntityTypeConfiguration<Player>
    {
        public void Configure(EntityTypeBuilder<Player> builder)
        {
            builder.ToTable("players");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.FirstName)
                .HasMaxLength(100)
                .IsRequired();

            builder.Property(x => x.LastName)
                .HasMaxLength(100)
                .IsRequired();

            builder.Property(x => x.PhotoUrl)
                .HasMaxLength(2048);

            builder.Property(x => x.DefaultPosition)
                .HasConversion<int>();

            builder.HasIndex(x => new { x.LastName, x.FirstName });
        }
    }
}
