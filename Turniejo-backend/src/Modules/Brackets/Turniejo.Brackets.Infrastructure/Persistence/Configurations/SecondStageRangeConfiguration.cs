using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Turniejo.Brackets.Domain.Entities;

namespace Turniejo.Brackets.Infrastructure.Persistence.Configurations
{
    public sealed class SecondStageRangeConfiguration : IEntityTypeConfiguration<SecondStageRange>
    {
        public void Configure(EntityTypeBuilder<SecondStageRange> builder)
        {
            builder.ToTable("second_stage_ranges");

            builder.HasKey(x => x.Id);

            builder.HasIndex(x => new { x.TournamentId, x.CategoryId });

            // Jeden zakres na grupę fazy 2.
            builder.HasIndex(x => x.GroupId)
                .IsUnique();
        }
    }
}
