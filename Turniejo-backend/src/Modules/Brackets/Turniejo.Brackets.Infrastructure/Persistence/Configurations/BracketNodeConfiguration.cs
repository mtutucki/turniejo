using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Turniejo.Brackets.Domain.Entities;

namespace Turniejo.Brackets.Infrastructure.Persistence.Configurations
{
    public sealed class BracketNodeConfiguration : IEntityTypeConfiguration<BracketNode>
    {
        public void Configure(EntityTypeBuilder<BracketNode> builder)
        {
            builder.ToTable("bracket_nodes");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.HomeSourceType)
                .HasConversion<int>();

            builder.Property(x => x.AwaySourceType)
                .HasConversion<int>();

            builder.Property(x => x.WinnerGoesToSlot)
                .HasConversion<int?>();

            builder.Property(x => x.LoserGoesToSlot)
                .HasConversion<int?>();

            builder.HasIndex(x => x.MatchId)
                .IsUnique()
                .HasFilter("\"MatchId\" IS NOT NULL");

            builder.HasOne<BracketRound>()
                .WithMany()
                .HasForeignKey(x => x.BracketRoundId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne<BracketNode>()
                .WithMany()
                .HasForeignKey(x => x.HomeSourceNodeId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.HasOne<BracketNode>()
                .WithMany()
                .HasForeignKey(x => x.AwaySourceNodeId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.HasOne<BracketNode>()
                .WithMany()
                .HasForeignKey(x => x.WinnerGoesToNodeId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.HasOne<BracketNode>()
                .WithMany()
                .HasForeignKey(x => x.LoserGoesToNodeId)
                .OnDelete(DeleteBehavior.SetNull);
        }
    }
}
