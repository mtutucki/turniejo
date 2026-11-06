using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Turniejo.Players.Domain.Entities;

namespace Turniejo.Players.Infrastructure.Persistence.Configurations
{
    public sealed class TeamPlayerConfiguration : IEntityTypeConfiguration<TeamPlayer>
    {
        public void Configure(EntityTypeBuilder<TeamPlayer> builder)
        {
            builder.ToTable("team_players");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.Status)
                .HasConversion<int>();

            // Numer w kadrze nie jest unikalny w bazie: kadry wypełnione ze starych składów mogą mieć powtórki.
            // Pilnuje tego handler przy dodawaniu i edycji.
            builder.HasIndex(x => new { x.TeamId, x.PlayerId })
                .IsUnique();

            builder.HasIndex(x => x.PlayerId);

            builder.HasOne<Player>()
                .WithMany()
                .HasForeignKey(x => x.PlayerId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
