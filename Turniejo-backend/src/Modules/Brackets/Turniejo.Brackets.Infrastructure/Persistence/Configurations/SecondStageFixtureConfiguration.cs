using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Turniejo.Brackets.Domain.Entities;

namespace Turniejo.Brackets.Infrastructure.Persistence.Configurations
{
    public sealed class SecondStageFixtureConfiguration : IEntityTypeConfiguration<SecondStageFixture>
    {
        public void Configure(EntityTypeBuilder<SecondStageFixture> builder)
        {
            builder.ToTable("second_stage_fixtures");

            builder.HasKey(x => x.Id);

            builder.HasIndex(x => x.TournamentId);

            builder.HasIndex(x => x.MatchId)
                .IsUnique()
                .HasFilter("\"MatchId\" IS NOT NULL");

            // Zmiana podziału usuwa miejsca, a z nimi zaplanowane mecze (podział zmienia się tylko bez terminarza).
            builder.HasOne<SecondStageSeat>()
                .WithMany()
                .HasForeignKey(x => x.HomeSeatId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne<SecondStageSeat>()
                .WithMany()
                .HasForeignKey(x => x.AwaySeatId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
