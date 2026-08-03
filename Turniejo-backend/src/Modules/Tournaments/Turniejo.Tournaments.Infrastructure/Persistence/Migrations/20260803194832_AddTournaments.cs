using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Turniejo.Tournaments.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTournaments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "tournaments",
                schema: "tournaments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Slug = table.Column<string>(type: "character varying(220)", maxLength: 220, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    StartAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EndAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Format = table.Column<int>(type: "integer", nullable: true),
                    TeamLimit = table.Column<int>(type: "integer", nullable: false),
                    IsPaid = table.Column<bool>(type: "boolean", nullable: false),
                    OrganizerCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    RefereeCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    CoachCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    ViewerCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Settings_PointsForWin = table.Column<int>(type: "integer", nullable: false),
                    Settings_PointsForDraw = table.Column<int>(type: "integer", nullable: false),
                    Settings_PointsForLoss = table.Column<int>(type: "integer", nullable: false),
                    Settings_MatchDurationMinutes = table.Column<int>(type: "integer", nullable: false),
                    Settings_BreakDurationMinutes = table.Column<int>(type: "integer", nullable: false),
                    Settings_IsThirdPlaceMatchEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    Settings_RefereeEditWindowMinutes = table.Column<int>(type: "integer", nullable: false),
                    Settings_WalkoverHomeScore = table.Column<int>(type: "integer", nullable: false),
                    Settings_WalkoverAwayScore = table.Column<int>(type: "integer", nullable: false),
                    Settings_CanCoachEditSquadAfterStart = table.Column<bool>(type: "boolean", nullable: false),
                    PublicSettings_IsPublic = table.Column<bool>(type: "boolean", nullable: false),
                    PublicSettings_ShowTeams = table.Column<bool>(type: "boolean", nullable: false),
                    PublicSettings_ShowPlayers = table.Column<bool>(type: "boolean", nullable: false),
                    PublicSettings_ShowSchedule = table.Column<bool>(type: "boolean", nullable: false),
                    PublicSettings_ShowStandings = table.Column<bool>(type: "boolean", nullable: false),
                    PublicSettings_ShowBracket = table.Column<bool>(type: "boolean", nullable: false),
                    PublicSettings_ShowLiveMatches = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tournaments", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_tournaments_OrganizationId_Status",
                schema: "tournaments",
                table: "tournaments",
                columns: new[] { "OrganizationId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_tournaments_Slug",
                schema: "tournaments",
                table: "tournaments",
                column: "Slug",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tournaments",
                schema: "tournaments");
        }
    }
}
