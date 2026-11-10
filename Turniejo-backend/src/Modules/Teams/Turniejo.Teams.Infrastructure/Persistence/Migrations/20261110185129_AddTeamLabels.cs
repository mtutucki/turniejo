using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Turniejo.Teams.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTeamLabels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<List<Guid>>(
                name: "LabelIds",
                schema: "teams",
                table: "teams",
                type: "uuid[]",
                nullable: false,
                // Istniejące drużyny dostają pustą listę etykiet.
                defaultValueSql: "'{}'::uuid[]");

            migrationBuilder.CreateTable(
                name: "team_labels",
                schema: "teams",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_team_labels", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_team_labels_OrganizationId",
                schema: "teams",
                table: "team_labels",
                column: "OrganizationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "team_labels",
                schema: "teams");

            migrationBuilder.DropColumn(
                name: "LabelIds",
                schema: "teams",
                table: "teams");
        }
    }
}
