using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Turniejo.Tournaments.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// AddTournamentPublicSettingsShowInDirectory wpisała wszystkim turniejom „na liście”, więc każdy
    /// publiczny turniej trafił na stronę główną bez wiedzy organizatora. Lista jest teraz opt-in:
    /// istniejące turnieje schodzą z niej, organizator dodaje turniej przełącznikiem w ustawieniach portalu.
    /// </summary>
    public partial class HideExistingTournamentsFromDirectory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<bool>(
                name: "PublicSettings_ShowInDirectory",
                schema: "tournaments",
                table: "tournaments",
                type: "boolean",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldDefaultValue: true);

            migrationBuilder.Sql(
                """UPDATE tournaments.tournaments SET "PublicSettings_ShowInDirectory" = false;""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Nie wiadomo, które turnieje były na liście przed Up, więc dane zostają bez zmian.
            migrationBuilder.AlterColumn<bool>(
                name: "PublicSettings_ShowInDirectory",
                schema: "tournaments",
                table: "tournaments",
                type: "boolean",
                nullable: false,
                defaultValue: true,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldDefaultValue: false);
        }
    }
}
