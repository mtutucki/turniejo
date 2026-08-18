using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Turniejo.Tournaments.Domain.Entities;

namespace Turniejo.Tournaments.Infrastructure.Persistence.Configurations
{
    public sealed class TournamentCategoryConfiguration : IEntityTypeConfiguration<TournamentCategory>
    {
        public void Configure(EntityTypeBuilder<TournamentCategory> builder)
        {
            builder.ToTable("tournament_categories");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.Name)
                .HasMaxLength(200)
                .IsRequired();

            builder.HasIndex(x => new { x.TournamentId, x.Name })
                .IsUnique();

            builder.HasOne<Tournament>()
                .WithMany()
                .HasForeignKey(x => x.TournamentId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}