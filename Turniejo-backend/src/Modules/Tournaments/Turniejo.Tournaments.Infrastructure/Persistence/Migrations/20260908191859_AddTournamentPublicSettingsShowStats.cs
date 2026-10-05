using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Turniejo.Tournaments.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTournamentPublicSettingsShowStats : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // defaultValue: true, nie false — TournamentPublicSettings.ShowStats domyślnie = true w
            // encji (jak każdy inny Show*), więc istniejące turnieje po migracji muszą dostać true,
            // inaczej wcześniej publiczne statystyki zniknęłyby im z dnia na dzień.
            migrationBuilder.AddColumn<bool>(
                name: "PublicSettings_ShowStats",
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
                name: "PublicSettings_ShowStats",
                schema: "tournaments",
                table: "tournaments");
        }
    }
}
