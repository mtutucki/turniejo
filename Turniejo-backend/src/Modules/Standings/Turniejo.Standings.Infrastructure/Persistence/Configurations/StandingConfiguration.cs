using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Turniejo.Standings.Domain.Entities;

namespace Turniejo.Standings.Infrastructure.Persistence.Configurations
{
    public sealed class StandingConfiguration : IEntityTypeConfiguration<Standing>
    {
        public void Configure(EntityTypeBuilder<Standing> builder)
        {
            builder.ToTable("standings");

            builder.HasKey(x => x.Id);

            builder.HasIndex(x => new { x.GroupId, x.TournamentTeamId }).IsUnique();
            builder.HasIndex(x => new { x.TournamentId, x.CategoryId, x.GroupId });
        }
    }
}
