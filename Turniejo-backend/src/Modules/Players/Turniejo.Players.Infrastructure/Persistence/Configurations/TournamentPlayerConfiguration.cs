using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Turniejo.Players.Domain.Entities;

namespace Turniejo.Players.Infrastructure.Persistence.Configurations
{
    public sealed class TournamentPlayerConfiguration : IEntityTypeConfiguration<TournamentPlayer>
    {
        public void Configure(EntityTypeBuilder<TournamentPlayer> builder)
        {
            builder.ToTable("tournament_players");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.Position)
                .HasConversion<int?>();

            builder.Property(x => x.Status)
                .HasConversion<int>();

            builder.HasIndex(x => new { x.TournamentTeamId, x.PlayerId })
                .IsUnique();

            builder.HasIndex(x => new { x.TournamentTeamId, x.ShirtNumber })
                .IsUnique()
                .HasFilter("\"ShirtNumber\" IS NOT NULL");

            builder.HasOne<Player>()
                .WithMany()
                .HasForeignKey(x => x.PlayerId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
