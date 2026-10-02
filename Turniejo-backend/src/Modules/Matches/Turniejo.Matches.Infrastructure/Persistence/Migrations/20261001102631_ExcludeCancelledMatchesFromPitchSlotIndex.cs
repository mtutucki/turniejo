using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Turniejo.Matches.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ExcludeCancelledMatchesFromPitchSlotIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_matches_PitchId_ScheduledAt",
                schema: "matches",
                table: "matches");

            migrationBuilder.CreateIndex(
                name: "IX_matches_PitchId_ScheduledAt",
                schema: "matches",
                table: "matches",
                columns: new[] { "PitchId", "ScheduledAt" },
                unique: true,
                filter: "\"Status\" <> 7");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_matches_PitchId_ScheduledAt",
                schema: "matches",
                table: "matches");

            migrationBuilder.CreateIndex(
                name: "IX_matches_PitchId_ScheduledAt",
                schema: "matches",
                table: "matches",
                columns: new[] { "PitchId", "ScheduledAt" },
                unique: true);
        }
    }
}
