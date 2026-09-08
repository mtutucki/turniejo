using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Turniejo.Matches.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPitchesAndMatches : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "pitches",
                schema: "matches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TournamentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pitches", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "matches",
                schema: "matches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TournamentId = table.Column<Guid>(type: "uuid", nullable: false),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: true),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: true),
                    BracketNodeId = table.Column<Guid>(type: "uuid", nullable: true),
                    PitchId = table.Column<Guid>(type: "uuid", nullable: true),
                    HomeTournamentTeamId = table.Column<Guid>(type: "uuid", nullable: false),
                    AwayTournamentTeamId = table.Column<Guid>(type: "uuid", nullable: false),
                    RefereeId = table.Column<Guid>(type: "uuid", nullable: true),
                    ScheduledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    StageType = table.Column<int>(type: "integer", nullable: false),
                    HomeScore = table.Column<int>(type: "integer", nullable: false),
                    AwayScore = table.Column<int>(type: "integer", nullable: false),
                    HomePenaltyScore = table.Column<int>(type: "integer", nullable: true),
                    AwayPenaltyScore = table.Column<int>(type: "integer", nullable: true),
                    ResultType = table.Column<int>(type: "integer", nullable: false),
                    WinnerTournamentTeamId = table.Column<Guid>(type: "uuid", nullable: true),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FinishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PausedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    TotalPausedSeconds = table.Column<int>(type: "integer", nullable: false),
                    EditLockedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_matches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_matches_pitches_PitchId",
                        column: x => x.PitchId,
                        principalSchema: "matches",
                        principalTable: "pitches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_matches_PitchId_ScheduledAt",
                schema: "matches",
                table: "matches",
                columns: new[] { "PitchId", "ScheduledAt" });

            migrationBuilder.CreateIndex(
                name: "IX_matches_RefereeId_ScheduledAt",
                schema: "matches",
                table: "matches",
                columns: new[] { "RefereeId", "ScheduledAt" });

            migrationBuilder.CreateIndex(
                name: "IX_matches_TournamentId_ScheduledAt",
                schema: "matches",
                table: "matches",
                columns: new[] { "TournamentId", "ScheduledAt" });

            migrationBuilder.CreateIndex(
                name: "IX_pitches_TournamentId_Name",
                schema: "matches",
                table: "pitches",
                columns: new[] { "TournamentId", "Name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "matches",
                schema: "matches");

            migrationBuilder.DropTable(
                name: "pitches",
                schema: "matches");
        }
    }
}
