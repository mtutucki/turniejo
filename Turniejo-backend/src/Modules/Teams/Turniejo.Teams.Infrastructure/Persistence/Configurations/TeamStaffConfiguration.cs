using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Turniejo.Teams.Domain.Entities;

namespace Turniejo.Teams.Infrastructure.Persistence.Configurations
{
    public sealed class TeamStaffConfiguration : IEntityTypeConfiguration<TeamStaff>
    {
        public void Configure(EntityTypeBuilder<TeamStaff> builder)
        {
            builder.ToTable("team_staff");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.Role)
                .HasConversion<int>();

            builder.HasIndex(x => new { x.TournamentTeamId, x.UserId, x.Role })
                .IsUnique();

            builder.HasOne<TournamentTeam>()
                .WithMany()
                .HasForeignKey(x => x.TournamentTeamId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
