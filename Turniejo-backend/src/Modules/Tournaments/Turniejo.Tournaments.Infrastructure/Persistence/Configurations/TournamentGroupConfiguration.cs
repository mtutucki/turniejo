using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Turniejo.Tournaments.Domain.Entities;

namespace Turniejo.Tournaments.Infrastructure.Persistence.Configurations
{
    public sealed class TournamentGroupConfiguration : IEntityTypeConfiguration<TournamentGroup>
    {
        public void Configure(EntityTypeBuilder<TournamentGroup> builder)
        {
            builder.ToTable("tournament_groups");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.Name)
                .HasMaxLength(200)
                .IsRequired();

            builder.HasIndex(x => new { x.CategoryId, x.Name })
                .IsUnique();

            builder.HasOne<Tournament>()
                .WithMany()
                .HasForeignKey(x => x.TournamentId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne<TournamentCategory>()
                .WithMany()
                .HasForeignKey(x => x.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}