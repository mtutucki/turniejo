using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Turniejo.Brackets.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSecondStageFixtures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "second_stage_fixtures",
                schema: "brackets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TournamentId = table.Column<Guid>(type: "uuid", nullable: false),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    HomeSeatId = table.Column<Guid>(type: "uuid", nullable: false),
                    AwaySeatId = table.Column<Guid>(type: "uuid", nullable: false),
                    Round = table.Column<int>(type: "integer", nullable: false),
                    PlannedPitchId = table.Column<Guid>(type: "uuid", nullable: true),
                    PlannedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PlannedRefereeId = table.Column<Guid>(type: "uuid", nullable: true),
                    MatchId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_second_stage_fixtures", x => x.Id);
                    table.ForeignKey(
                        name: "FK_second_stage_fixtures_second_stage_seats_AwaySeatId",
                        column: x => x.AwaySeatId,
                        principalSchema: "brackets",
                        principalTable: "second_stage_seats",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_second_stage_fixtures_second_stage_seats_HomeSeatId",
                        column: x => x.HomeSeatId,
                        principalSchema: "brackets",
                        principalTable: "second_stage_seats",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_second_stage_fixtures_AwaySeatId",
                schema: "brackets",
                table: "second_stage_fixtures",
                column: "AwaySeatId");

            migrationBuilder.CreateIndex(
                name: "IX_second_stage_fixtures_HomeSeatId",
                schema: "brackets",
                table: "second_stage_fixtures",
                column: "HomeSeatId");

            migrationBuilder.CreateIndex(
                name: "IX_second_stage_fixtures_MatchId",
                schema: "brackets",
                table: "second_stage_fixtures",
                column: "MatchId",
                unique: true,
                filter: "\"MatchId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_second_stage_fixtures_TournamentId",
                schema: "brackets",
                table: "second_stage_fixtures",
                column: "TournamentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "second_stage_fixtures",
                schema: "brackets");
        }
    }
}
