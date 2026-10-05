using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Turniejo.Tournaments.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FixTieBreakerRuleCascadeDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_tie_breaker_rules_tournaments_TournamentId",
                schema: "tournaments",
                table: "tie_breaker_rules");

            migrationBuilder.AddForeignKey(
                name: "FK_tie_breaker_rules_tournaments_TournamentId",
                schema: "tournaments",
                table: "tie_breaker_rules",
                column: "TournamentId",
                principalSchema: "tournaments",
                principalTable: "tournaments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_tie_breaker_rules_tournaments_TournamentId",
                schema: "tournaments",
                table: "tie_breaker_rules");

            migrationBuilder.AddForeignKey(
                name: "FK_tie_breaker_rules_tournaments_TournamentId",
                schema: "tournaments",
                table: "tie_breaker_rules",
                column: "TournamentId",
                principalSchema: "tournaments",
                principalTable: "tournaments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
