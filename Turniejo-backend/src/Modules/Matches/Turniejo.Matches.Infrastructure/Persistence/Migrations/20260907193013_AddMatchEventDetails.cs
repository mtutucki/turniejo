using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Turniejo.Matches.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMatchEventDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_match_events_matches_MatchId",
                schema: "matches",
                table: "match_events");

            migrationBuilder.DropIndex(
                name: "IX_match_events_MatchId_IsDeleted",
                schema: "matches",
                table: "match_events");

            migrationBuilder.DropColumn(
                name: "Description",
                schema: "matches",
                table: "match_events");

            migrationBuilder.RenameColumn(
                name: "EventType",
                schema: "matches",
                table: "match_events",
                newName: "Type");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedAt",
                schema: "matches",
                table: "match_events",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DeletedByUserId",
                schema: "matches",
                table: "match_events",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Minute",
                schema: "matches",
                table: "match_events",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "PlayerId",
                schema: "matches",
                table: "match_events",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RelatedPlayerId",
                schema: "matches",
                table: "match_events",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Second",
                schema: "matches",
                table: "match_events",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TournamentTeamId",
                schema: "matches",
                table: "match_events",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_match_events_MatchId_Minute_CreatedAt",
                schema: "matches",
                table: "match_events",
                columns: new[] { "MatchId", "Minute", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_match_events_PlayerId_Type_IsDeleted",
                schema: "matches",
                table: "match_events",
                columns: new[] { "PlayerId", "Type", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_match_events_RelatedPlayerId_Type_IsDeleted",
                schema: "matches",
                table: "match_events",
                columns: new[] { "RelatedPlayerId", "Type", "IsDeleted" });

            migrationBuilder.AddForeignKey(
                name: "FK_match_events_matches_MatchId",
                schema: "matches",
                table: "match_events",
                column: "MatchId",
                principalSchema: "matches",
                principalTable: "matches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_match_events_matches_MatchId",
                schema: "matches",
                table: "match_events");

            migrationBuilder.DropIndex(
                name: "IX_match_events_MatchId_Minute_CreatedAt",
                schema: "matches",
                table: "match_events");

            migrationBuilder.DropIndex(
                name: "IX_match_events_PlayerId_Type_IsDeleted",
                schema: "matches",
                table: "match_events");

            migrationBuilder.DropIndex(
                name: "IX_match_events_RelatedPlayerId_Type_IsDeleted",
                schema: "matches",
                table: "match_events");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "matches",
                table: "match_events");

            migrationBuilder.DropColumn(
                name: "DeletedByUserId",
                schema: "matches",
                table: "match_events");

            migrationBuilder.DropColumn(
                name: "Minute",
                schema: "matches",
                table: "match_events");

            migrationBuilder.DropColumn(
                name: "PlayerId",
                schema: "matches",
                table: "match_events");

            migrationBuilder.DropColumn(
                name: "RelatedPlayerId",
                schema: "matches",
                table: "match_events");

            migrationBuilder.DropColumn(
                name: "Second",
                schema: "matches",
                table: "match_events");

            migrationBuilder.DropColumn(
                name: "TournamentTeamId",
                schema: "matches",
                table: "match_events");

            migrationBuilder.RenameColumn(
                name: "Type",
                schema: "matches",
                table: "match_events",
                newName: "EventType");

            migrationBuilder.AddColumn<string>(
                name: "Description",
                schema: "matches",
                table: "match_events",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_match_events_MatchId_IsDeleted",
                schema: "matches",
                table: "match_events",
                columns: new[] { "MatchId", "IsDeleted" });

            migrationBuilder.AddForeignKey(
                name: "FK_match_events_matches_MatchId",
                schema: "matches",
                table: "match_events",
                column: "MatchId",
                principalSchema: "matches",
                principalTable: "matches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
