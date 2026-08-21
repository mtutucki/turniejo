using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Turniejo.Tournaments.Domain.Entities;

namespace Turniejo.Tournaments.Infrastructure.Persistence.Configurations
{
    public sealed class TieBreakerRuleConfiguration : IEntityTypeConfiguration<TieBreakerRule>
    {
        public void Configure(EntityTypeBuilder<TieBreakerRule> builder)
        {
            builder.ToTable("tie_breaker_rules");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.Type)
                .HasConversion<int>();

            builder.HasIndex(x => new { x.TournamentId, x.Order })
                .IsUnique();

            builder.HasOne<Tournament>()
                .WithMany()
                .HasForeignKey(x => x.TournamentId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}