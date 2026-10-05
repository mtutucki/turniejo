using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Turniejo.Brackets.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBracketNodePlannedSlot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PlannedAt",
                schema: "brackets",
                table: "bracket_nodes",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PlannedPitchId",
                schema: "brackets",
                table: "bracket_nodes",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PlannedAt",
                schema: "brackets",
                table: "bracket_nodes");

            migrationBuilder.DropColumn(
                name: "PlannedPitchId",
                schema: "brackets",
                table: "bracket_nodes");
        }
    }
}
