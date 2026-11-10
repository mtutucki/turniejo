using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Turniejo.Brackets.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSecondStageSeats : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "second_stage_seats",
                schema: "brackets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TournamentId = table.Column<Guid>(type: "uuid", nullable: false),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    SourceGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourcePosition = table.Column<int>(type: "integer", nullable: false),
                    TournamentTeamId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_second_stage_seats", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_second_stage_seats_SourceGroupId_SourcePosition",
                schema: "brackets",
                table: "second_stage_seats",
                columns: new[] { "SourceGroupId", "SourcePosition" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_second_stage_seats_TournamentId_CategoryId",
                schema: "brackets",
                table: "second_stage_seats",
                columns: new[] { "TournamentId", "CategoryId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "second_stage_seats",
                schema: "brackets");
        }
    }
}
