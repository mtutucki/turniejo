using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Turniejo.Brackets.Domain.Entities;

namespace Turniejo.Brackets.Infrastructure.Persistence.Configurations
{
    public sealed class SecondStageSeatConfiguration : IEntityTypeConfiguration<SecondStageSeat>
    {
        public void Configure(EntityTypeBuilder<SecondStageSeat> builder)
        {
            builder.ToTable("second_stage_seats");

            builder.HasKey(x => x.Id);

            builder.HasIndex(x => new { x.TournamentId, x.CategoryId });

            // Jedno miejsce w tabeli fazy 1 trafia najwyżej do jednej grupy fazy 2.
            builder.HasIndex(x => new { x.SourceGroupId, x.SourcePosition })
                .IsUnique();
        }
    }
}
