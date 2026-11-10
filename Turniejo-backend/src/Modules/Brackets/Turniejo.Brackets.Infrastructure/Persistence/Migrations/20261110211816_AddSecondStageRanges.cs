using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Turniejo.Brackets.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSecondStageRanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "second_stage_ranges",
                schema: "brackets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TournamentId = table.Column<Guid>(type: "uuid", nullable: false),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    FromPosition = table.Column<int>(type: "integer", nullable: false),
                    ToPosition = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_second_stage_ranges", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_second_stage_ranges_GroupId",
                schema: "brackets",
                table: "second_stage_ranges",
                column: "GroupId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_second_stage_ranges_TournamentId_CategoryId",
                schema: "brackets",
                table: "second_stage_ranges",
                columns: new[] { "TournamentId", "CategoryId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "second_stage_ranges",
                schema: "brackets");
        }
    }
}
