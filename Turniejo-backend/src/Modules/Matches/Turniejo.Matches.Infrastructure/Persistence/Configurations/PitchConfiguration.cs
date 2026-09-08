using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Turniejo.Matches.Domain.Entities;

namespace Turniejo.Matches.Infrastructure.Persistence.Configurations
{
    public sealed class PitchConfiguration : IEntityTypeConfiguration<Pitch>
    {
        public void Configure(EntityTypeBuilder<Pitch> builder)
        {
            builder.ToTable("pitches");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.Name)
                .HasMaxLength(100)
                .IsRequired();

            builder.Property(x => x.Description)
                .HasMaxLength(500);

            builder.HasIndex(x => new { x.TournamentId, x.Name })
                .IsUnique();
        }
    }
}
