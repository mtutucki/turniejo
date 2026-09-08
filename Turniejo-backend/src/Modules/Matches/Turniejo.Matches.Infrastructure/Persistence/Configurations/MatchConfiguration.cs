using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Turniejo.Matches.Domain.Entities;

namespace Turniejo.Matches.Infrastructure.Persistence.Configurations
{
    public sealed class MatchConfiguration : IEntityTypeConfiguration<Match>
    {
        public void Configure(EntityTypeBuilder<Match> builder)
        {
            builder.ToTable("matches");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.Status)
                .HasConversion<int>();

            builder.Property(x => x.StageType)
                .HasConversion<int>();

            builder.Property(x => x.ResultType)
                .HasConversion<int>();

            builder.HasIndex(x => new { x.TournamentId, x.ScheduledAt });
            builder.HasIndex(x => new { x.PitchId, x.ScheduledAt });
            builder.HasIndex(x => new { x.RefereeId, x.ScheduledAt });

            builder.HasOne<Pitch>()
                .WithMany()
                .HasForeignKey(x => x.PitchId)
                .OnDelete(DeleteBehavior.SetNull);
        }
    }
}
