using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Turniejo.Tournaments.Domain.Entities;

namespace Turniejo.Tournaments.Infrastructure.Persistence.Configurations
{
    public sealed class TournamentConfiguration : IEntityTypeConfiguration<Tournament>
    {
        public void Configure(EntityTypeBuilder<Tournament> builder)
        {
            builder.ToTable("tournaments");

            builder.HasKey(t => t.Id);

            builder.Property(t => t.Name)
                .HasMaxLength(200)
                .IsRequired();

            builder.HasIndex(x => x.Slug)
                .IsUnique();

            builder.Property(x => x.Slug)
                .HasMaxLength(220)
                .IsRequired();

            builder.Property(x => x.Description)
                .HasMaxLength(500);

            builder.Property(x => x.Status)
                .HasConversion<int>();

            builder.Property(x => x.Format)
                .HasConversion<int?>();

            builder.Property(x => x.OrganizerCode)
                .HasMaxLength(10);

            builder.Property(x => x.RefereeCode)
                .HasMaxLength(10);

            builder.Property(x => x.CoachCode)
                .HasMaxLength(10);

            builder.Property(x => x.ViewerCode)
                .HasMaxLength(10);

            builder.HasIndex(x => new { x.OrganizationId, x.Status });

            builder.OwnsOne(x => x.Settings, s =>
            {
                s.WithOwner().HasForeignKey("TournamentId");
            });
            builder.OwnsOne(x => x.PublicSettings, s =>
            {
                s.WithOwner().HasForeignKey("TournamentId");
            });


        }
    }
}
