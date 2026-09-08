using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Turniejo.Teams.Domain.Entities;

namespace Turniejo.Teams.Infrastructure.Persistence.Configurations
{
    public sealed class TeamConfiguration : IEntityTypeConfiguration<Team>
    {
        public void Configure(EntityTypeBuilder<Team> builder)
        {
            builder.ToTable("teams");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.Name)
                .HasMaxLength(200)
                .IsRequired();

            builder.Property(x => x.ShortName)
                .HasMaxLength(20);

            builder.Property(x => x.LogoUrl)
                .HasMaxLength(2048);

            builder.HasIndex(x => new { x.OrganizationId, x.Name });
        }
    }
}
