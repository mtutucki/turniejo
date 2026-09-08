using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Turniejo.Tournaments.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTournamentSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Settings_BreakDurationMinutes",
                schema: "tournaments",
                table: "tournaments");

            migrationBuilder.DropColumn(
                name: "Settings_CanCoachEditSquadAfterStart",
                schema: "tournaments",
                table: "tournaments");

            migrationBuilder.DropColumn(
                name: "Settings_IsThirdPlaceMatchEnabled",
                schema: "tournaments",
                table: "tournaments");

            migrationBuilder.DropColumn(
                name: "Settings_MatchDurationMinutes",
                schema: "tournaments",
                table: "tournaments");

            migrationBuilder.DropColumn(
                name: "Settings_PointsForDraw",
                schema: "tournaments",
                table: "tournaments");

            migrationBuilder.DropColumn(
                name: "Settings_PointsForLoss",
                schema: "tournaments",
                table: "tournaments");

            migrationBuilder.DropColumn(
                name: "Settings_PointsForWin",
                schema: "tournaments",
                table: "tournaments");

            migrationBuilder.DropColumn(
                name: "Settings_RefereeEditWindowMinutes",
                schema: "tournaments",
                table: "tournaments");

            migrationBuilder.DropColumn(
                name: "Settings_WalkoverAwayScore",
                schema: "tournaments",
                table: "tournaments");

            migrationBuilder.DropColumn(
                name: "Settings_WalkoverHomeScore",
                schema: "tournaments",
                table: "tournaments");

            migrationBuilder.CreateTable(
                name: "tournament_settings",
                schema: "tournaments",
                columns: table => new
                {
                    TournamentId = table.Column<Guid>(type: "uuid", nullable: false),
                    PointsForWin = table.Column<int>(type: "integer", nullable: false),
                    PointsForDraw = table.Column<int>(type: "integer", nullable: false),
                    PointsForLoss = table.Column<int>(type: "integer", nullable: false),
                    MatchDurationMinutes = table.Column<int>(type: "integer", nullable: false),
                    BreakDurationMinutes = table.Column<int>(type: "integer", nullable: false),
                    IsThirdPlaceMatchEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    RefereeEditWindowMinutes = table.Column<int>(type: "integer", nullable: false),
                    WalkoverHomeScore = table.Column<int>(type: "integer", nullable: false),
                    WalkoverAwayScore = table.Column<int>(type: "integer", nullable: false),
                    CanCoachEditSquadAfterStart = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tournament_settings", x => x.TournamentId);
                    table.ForeignKey(
                        name: "FK_tournament_settings_tournaments_TournamentId",
                        column: x => x.TournamentId,
                        principalSchema: "tournaments",
                        principalTable: "tournaments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tournament_settings",
                schema: "tournaments");

            migrationBuilder.AddColumn<int>(
                name: "Settings_BreakDurationMinutes",
                schema: "tournaments",
                table: "tournaments",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "Settings_CanCoachEditSquadAfterStart",
                schema: "tournaments",
                table: "tournaments",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "Settings_IsThirdPlaceMatchEnabled",
                schema: "tournaments",
                table: "tournaments",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "Settings_MatchDurationMinutes",
                schema: "tournaments",
                table: "tournaments",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Settings_PointsForDraw",
                schema: "tournaments",
                table: "tournaments",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Settings_PointsForLoss",
                schema: "tournaments",
                table: "tournaments",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Settings_PointsForWin",
                schema: "tournaments",
                table: "tournaments",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Settings_RefereeEditWindowMinutes",
                schema: "tournaments",
                table: "tournaments",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Settings_WalkoverAwayScore",
                schema: "tournaments",
                table: "tournaments",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Settings_WalkoverHomeScore",
                schema: "tournaments",
                table: "tournaments",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }
    }
}
