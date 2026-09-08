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

            // Cascade, nie Restrict (rozbieżność ze specem 03_RELACJE) — TieBreakerRule to część
            // konfiguracji turnieju, tego samego rodzaju co TournamentSettings/PublicSettings
            // (też Cascade), a nie niezależny byt, którego istnienie powinno blokować kasowanie turnieju.
            builder.HasOne<Tournament>()
                .WithMany()
                .HasForeignKey(x => x.TournamentId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}