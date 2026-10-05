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

            // Usunięte (anulowane) turnieje znikają ze wszystkich zapytań modułu — także z ITournamentModule,
            // więc inne moduły widzą je jak nieistniejące.
            builder.HasQueryFilter(t => t.DeletedAt == null);

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
                s.ToTable("tournament_settings");
                s.HasKey(ts => ts.TournamentId);
                s.WithOwner().HasForeignKey("TournamentId");

                var ownership = s.WithOwner().HasForeignKey("TournamentId");
                ownership.Metadata.DeleteBehavior = DeleteBehavior.Cascade;

                s.Property(ts => ts.PointsForWin).IsRequired();
                s.Property(ts => ts.PointsForDraw).IsRequired();
                s.Property(ts => ts.PointsForLoss).IsRequired();
                s.Property(ts => ts.MatchDurationMinutes).IsRequired();
                s.Property(ts => ts.BreakDurationMinutes).IsRequired();
                s.Property(ts => ts.IsThirdPlaceMatchEnabled).IsRequired();
                s.Property(ts => ts.RefereeEditWindowMinutes).IsRequired();
                s.Property(ts => ts.WalkoverHomeScore).IsRequired();
                s.Property(ts => ts.WalkoverAwayScore).IsRequired();
                s.Property(ts => ts.CanCoachEditSquadAfterStart).IsRequired();
            });

            builder.OwnsOne(x => x.PublicSettings, s =>
            {
                s.WithOwner().HasForeignKey("TournamentId");
            });


        }
    }
}
