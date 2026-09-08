using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Turniejo.Teams.Domain.Entities;

namespace Turniejo.Teams.Infrastructure.Persistence.Configurations
{
    public sealed class TournamentTeamConfiguration : IEntityTypeConfiguration<TournamentTeam>
    {
        public void Configure(EntityTypeBuilder<TournamentTeam> builder)
        {
            builder.ToTable("tournament_teams");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.RegistrationStatus)
                .HasConversion<int>();

            builder.Property(x => x.PaymentStatus)
                .HasConversion<int>();

            builder.HasIndex(x => new { x.TournamentId, x.TeamId, x.CategoryId })
                .IsUnique();

            builder.HasIndex(x => new { x.TournamentId, x.RegistrationStatus });

            builder.HasOne<Team>()
                .WithMany()
                .HasForeignKey(x => x.TeamId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
