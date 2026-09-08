using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Turniejo.Brackets.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOutboxBackoff : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "NextAttemptAt",
                schema: "brackets",
                table: "outbox_messages",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NextAttemptAt",
                schema: "brackets",
                table: "outbox_messages");
        }
    }
}
