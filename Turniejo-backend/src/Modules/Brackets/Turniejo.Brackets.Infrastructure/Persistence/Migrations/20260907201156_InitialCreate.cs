using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Turniejo.Brackets.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "brackets");

            migrationBuilder.CreateTable(
                name: "brackets",
                schema: "brackets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TournamentId = table.Column<Guid>(type: "uuid", nullable: false),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_brackets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "outbox_messages",
                schema: "brackets",
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
                name: "bracket_rounds",
                schema: "brackets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BracketId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoundNumber = table.Column<int>(type: "integer", nullable: false),
                    RoundType = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bracket_rounds", x => x.Id);
                    table.ForeignKey(
                        name: "FK_bracket_rounds_brackets_BracketId",
                        column: x => x.BracketId,
                        principalSchema: "brackets",
                        principalTable: "brackets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "bracket_nodes",
                schema: "brackets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BracketRoundId = table.Column<Guid>(type: "uuid", nullable: false),
                    MatchId = table.Column<Guid>(type: "uuid", nullable: true),
                    HomeSourceType = table.Column<int>(type: "integer", nullable: false),
                    HomeSourceGroupId = table.Column<Guid>(type: "uuid", nullable: true),
                    HomeSourcePosition = table.Column<int>(type: "integer", nullable: true),
                    HomeSourceNodeId = table.Column<Guid>(type: "uuid", nullable: true),
                    HomeTournamentTeamId = table.Column<Guid>(type: "uuid", nullable: true),
                    AwaySourceType = table.Column<int>(type: "integer", nullable: false),
                    AwaySourceGroupId = table.Column<Guid>(type: "uuid", nullable: true),
                    AwaySourcePosition = table.Column<int>(type: "integer", nullable: true),
                    AwaySourceNodeId = table.Column<Guid>(type: "uuid", nullable: true),
                    AwayTournamentTeamId = table.Column<Guid>(type: "uuid", nullable: true),
                    WinnerGoesToNodeId = table.Column<Guid>(type: "uuid", nullable: true),
                    WinnerGoesToSlot = table.Column<int>(type: "integer", nullable: true),
                    LoserGoesToNodeId = table.Column<Guid>(type: "uuid", nullable: true),
                    LoserGoesToSlot = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bracket_nodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_bracket_nodes_bracket_nodes_AwaySourceNodeId",
                        column: x => x.AwaySourceNodeId,
                        principalSchema: "brackets",
                        principalTable: "bracket_nodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_bracket_nodes_bracket_nodes_HomeSourceNodeId",
                        column: x => x.HomeSourceNodeId,
                        principalSchema: "brackets",
                        principalTable: "bracket_nodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_bracket_nodes_bracket_nodes_LoserGoesToNodeId",
                        column: x => x.LoserGoesToNodeId,
                        principalSchema: "brackets",
                        principalTable: "bracket_nodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_bracket_nodes_bracket_nodes_WinnerGoesToNodeId",
                        column: x => x.WinnerGoesToNodeId,
                        principalSchema: "brackets",
                        principalTable: "bracket_nodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_bracket_nodes_bracket_rounds_BracketRoundId",
                        column: x => x.BracketRoundId,
                        principalSchema: "brackets",
                        principalTable: "bracket_rounds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_bracket_nodes_AwaySourceNodeId",
                schema: "brackets",
                table: "bracket_nodes",
                column: "AwaySourceNodeId");

            migrationBuilder.CreateIndex(
                name: "IX_bracket_nodes_BracketRoundId",
                schema: "brackets",
                table: "bracket_nodes",
                column: "BracketRoundId");

            migrationBuilder.CreateIndex(
                name: "IX_bracket_nodes_HomeSourceNodeId",
                schema: "brackets",
                table: "bracket_nodes",
                column: "HomeSourceNodeId");

            migrationBuilder.CreateIndex(
                name: "IX_bracket_nodes_LoserGoesToNodeId",
                schema: "brackets",
                table: "bracket_nodes",
                column: "LoserGoesToNodeId");

            migrationBuilder.CreateIndex(
                name: "IX_bracket_nodes_MatchId",
                schema: "brackets",
                table: "bracket_nodes",
                column: "MatchId",
                unique: true,
                filter: "\"MatchId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_bracket_nodes_WinnerGoesToNodeId",
                schema: "brackets",
                table: "bracket_nodes",
                column: "WinnerGoesToNodeId");

            migrationBuilder.CreateIndex(
                name: "IX_bracket_rounds_BracketId_RoundNumber_RoundType",
                schema: "brackets",
                table: "bracket_rounds",
                columns: new[] { "BracketId", "RoundNumber", "RoundType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_brackets_TournamentId_CategoryId",
                schema: "brackets",
                table: "brackets",
                columns: new[] { "TournamentId", "CategoryId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_outbox_messages_type",
                schema: "brackets",
                table: "outbox_messages",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "IX_outbox_messages_unprocessed",
                schema: "brackets",
                table: "outbox_messages",
                column: "ProcessedAt",
                filter: "\"ProcessedAt\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "bracket_nodes",
                schema: "brackets");

            migrationBuilder.DropTable(
                name: "outbox_messages",
                schema: "brackets");

            migrationBuilder.DropTable(
                name: "bracket_rounds",
                schema: "brackets");

            migrationBuilder.DropTable(
                name: "brackets",
                schema: "brackets");
        }
    }
}
