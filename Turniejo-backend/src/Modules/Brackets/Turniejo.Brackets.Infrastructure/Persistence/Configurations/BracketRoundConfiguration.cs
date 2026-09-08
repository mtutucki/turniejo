using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Turniejo.Brackets.Domain.Entities;

namespace Turniejo.Brackets.Infrastructure.Persistence.Configurations
{
    public sealed class BracketRoundConfiguration : IEntityTypeConfiguration<BracketRound>
    {
        public void Configure(EntityTypeBuilder<BracketRound> builder)
        {
            builder.ToTable("bracket_rounds");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.RoundType)
                .HasConversion<int>();

            builder.Property(x => x.Name)
                .HasMaxLength(100)
                .IsRequired();

            builder.HasIndex(x => new { x.BracketId, x.RoundNumber, x.RoundType }).IsUnique();

            builder.HasOne<Bracket>()
                .WithMany()
                .HasForeignKey(x => x.BracketId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
