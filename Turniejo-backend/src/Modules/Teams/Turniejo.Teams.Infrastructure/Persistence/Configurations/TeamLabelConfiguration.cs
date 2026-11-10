using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Turniejo.Teams.Domain.Entities;

namespace Turniejo.Teams.Infrastructure.Persistence.Configurations
{
    public sealed class TeamLabelConfiguration : IEntityTypeConfiguration<TeamLabel>
    {
        public void Configure(EntityTypeBuilder<TeamLabel> builder)
        {
            builder.ToTable("team_labels");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.Name)
                .HasMaxLength(TeamLabel.NameMaxLength)
                .IsRequired();

            builder.HasIndex(x => x.OrganizationId);
        }
    }
}
