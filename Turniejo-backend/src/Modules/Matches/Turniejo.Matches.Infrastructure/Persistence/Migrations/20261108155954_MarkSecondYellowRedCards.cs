using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Turniejo.Matches.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MarkSecondYellowRedCards : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsSecondYellow",
                schema: "matches",
                table: "match_events",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsSecondYellow",
                schema: "matches",
                table: "match_events");
        }
    }
}
