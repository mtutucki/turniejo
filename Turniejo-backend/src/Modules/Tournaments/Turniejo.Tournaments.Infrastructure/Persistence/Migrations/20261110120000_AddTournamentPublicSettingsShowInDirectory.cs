using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Turniejo.Tournaments.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTournamentPublicSettingsShowInDirectory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // defaultValue: true: publiczne turnieje sprzed przełącznika zostają na liście turniejów.
            migrationBuilder.AddColumn<bool>(
                name: "PublicSettings_ShowInDirectory",
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
                name: "PublicSettings_ShowInDirectory",
                schema: "tournaments",
                table: "tournaments");
        }
    }
}
