using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Turniejo.Organizations.Domain.Entities;

namespace Turniejo.Organizations.Infrastructure.Persistence.Configurations;

public sealed class OrganizationConfiguration : IEntityTypeConfiguration<Organization>
{
    public void Configure(EntityTypeBuilder<Organization> builder)
    {
        builder.ToTable("organizations");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Slug).HasMaxLength(220).IsRequired();
        builder.HasIndex(x => x.Slug).IsUnique().HasDatabaseName("IX_organizations_slug");
        // V1: 1 user = 1 organizacja (decyzja #21 w arkuszu) — indeks unikalny to ostatnia linia
        // obrony przeciw wyścigowi przy równoległych żądaniach tworzenia organizacji.
        builder.HasIndex(x => x.OwnerUserId).IsUnique().HasDatabaseName("IX_organizations_owner");
    }
}