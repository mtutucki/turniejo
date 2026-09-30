using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Turniejo.Tournaments.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTournamentPublicSettingsAbbreviateNames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // defaultValue: true także dla istniejących turniejów — RODO (privacy by default): zawodnicy
            // to zwykle dzieci, a pełne nazwiska organizator może świadomie włączyć w ustawieniach portalu.
            migrationBuilder.AddColumn<bool>(
                name: "PublicSettings_AbbreviatePlayerNames",
                schema: "tournaments",
                table: "tournaments",
                type: "boolean",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PublicSettings_AbbreviatePlayerNames",
                schema: "tournaments",
                table: "tournaments");
        }
    }
}
