using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Turniejo.Matches.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMatchAwaitingDecisionSince : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "AwaitingDecisionSince",
                schema: "matches",
                table: "matches",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AwaitingDecisionSince",
                schema: "matches",
                table: "matches");
        }
    }
}
