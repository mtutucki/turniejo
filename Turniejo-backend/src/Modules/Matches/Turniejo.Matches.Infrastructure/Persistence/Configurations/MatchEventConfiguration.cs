using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Turniejo.Matches.Domain.Entities;

namespace Turniejo.Matches.Infrastructure.Persistence.Configurations
{
    public sealed class MatchEventConfiguration : IEntityTypeConfiguration<MatchEvent>
    {
        public void Configure(EntityTypeBuilder<MatchEvent> builder)
        {
            builder.ToTable("match_events");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.Type)
                .HasConversion<int>();

            builder.HasIndex(x => new { x.MatchId, x.Minute, x.CreatedAt });
            builder.HasIndex(x => new { x.PlayerId, x.Type, x.IsDeleted });
            builder.HasIndex(x => new { x.RelatedPlayerId, x.Type, x.IsDeleted });

            builder.HasOne<Match>()
                .WithMany()
                .HasForeignKey(x => x.MatchId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
