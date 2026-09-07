using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Turniejo.Players.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "players");

            migrationBuilder.CreateTable(
                name: "outbox_messages",
                schema: "players",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Payload = table.Column<string>(type: "jsonb", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ProcessedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RetryCount = table.Column<int>(type: "integer", nullable: false),
                    Error = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_outbox_messages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "players",
                schema: "players",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    FirstName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    LastName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    BirthDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DefaultPosition = table.Column<int>(type: "integer", nullable: false),
                    PhotoUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_players", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "tournament_players",
                schema: "players",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TournamentTeamId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlayerId = table.Column<Guid>(type: "uuid", nullable: false),
                    ShirtNumber = table.Column<int>(type: "integer", nullable: true),
                    Position = table.Column<int>(type: "integer", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tournament_players", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tournament_players_players_PlayerId",
                        column: x => x.PlayerId,
                        principalSchema: "players",
                        principalTable: "players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_outbox_messages_type",
                schema: "players",
                table: "outbox_messages",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "IX_outbox_messages_unprocessed",
                schema: "players",
                table: "outbox_messages",
                column: "ProcessedAt",
                filter: "\"ProcessedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_players_LastName_FirstName",
                schema: "players",
                table: "players",
                columns: new[] { "LastName", "FirstName" });

            migrationBuilder.CreateIndex(
                name: "IX_tournament_players_PlayerId",
                schema: "players",
                table: "tournament_players",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_tournament_players_TournamentTeamId_PlayerId",
                schema: "players",
                table: "tournament_players",
                columns: new[] { "TournamentTeamId", "PlayerId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tournament_players_TournamentTeamId_ShirtNumber",
                schema: "players",
                table: "tournament_players",
                columns: new[] { "TournamentTeamId", "ShirtNumber" },
                unique: true,
                filter: "\"ShirtNumber\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "outbox_messages",
                schema: "players");

            migrationBuilder.DropTable(
                name: "tournament_players",
                schema: "players");

            migrationBuilder.DropTable(
                name: "players",
                schema: "players");
        }
    }
}
