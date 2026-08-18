using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Turniejo.Tournaments.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCategoriesGroupsTieBreakerRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "tie_breaker_rules",
                schema: "tournaments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TournamentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tie_breaker_rules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tie_breaker_rules_tournaments_TournamentId",
                        column: x => x.TournamentId,
                        principalSchema: "tournaments",
                        principalTable: "tournaments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "tournament_categories",
                schema: "tournaments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TournamentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    MinBirthDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    MaxBirthDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tournament_categories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tournament_categories_tournaments_TournamentId",
                        column: x => x.TournamentId,
                        principalSchema: "tournaments",
                        principalTable: "tournaments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "tournament_groups",
                schema: "tournaments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TournamentId = table.Column<Guid>(type: "uuid", nullable: false),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tournament_groups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tournament_groups_tournament_categories_CategoryId",
                        column: x => x.CategoryId,
                        principalSchema: "tournaments",
                        principalTable: "tournament_categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_tournament_groups_tournaments_TournamentId",
                        column: x => x.TournamentId,
                        principalSchema: "tournaments",
                        principalTable: "tournaments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_tie_breaker_rules_TournamentId_Order",
                schema: "tournaments",
                table: "tie_breaker_rules",
                columns: new[] { "TournamentId", "Order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tournament_categories_TournamentId_Name",
                schema: "tournaments",
                table: "tournament_categories",
                columns: new[] { "TournamentId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tournament_groups_CategoryId_Name",
                schema: "tournaments",
                table: "tournament_groups",
                columns: new[] { "CategoryId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tournament_groups_TournamentId",
                schema: "tournaments",
                table: "tournament_groups",
                column: "TournamentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tie_breaker_rules",
                schema: "tournaments");

            migrationBuilder.DropTable(
                name: "tournament_groups",
                schema: "tournaments");

            migrationBuilder.DropTable(
                name: "tournament_categories",
                schema: "tournaments");
        }
    }
}
