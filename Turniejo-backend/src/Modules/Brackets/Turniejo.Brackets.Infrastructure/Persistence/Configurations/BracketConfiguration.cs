using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Turniejo.Brackets.Domain.Entities;

namespace Turniejo.Brackets.Infrastructure.Persistence.Configurations
{
    public sealed class BracketConfiguration : IEntityTypeConfiguration<Bracket>
    {
        public void Configure(EntityTypeBuilder<Bracket> builder)
        {
            builder.ToTable("brackets");

            builder.HasKey(x => x.Id);

            builder.HasIndex(x => new { x.TournamentId, x.CategoryId }).IsUnique();
        }
    }
}
