using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Turniejo.Players.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTeamPlayers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "team_players",
                schema: "players",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TeamId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlayerId = table.Column<Guid>(type: "uuid", nullable: false),
                    ShirtNumber = table.Column<int>(type: "integer", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    LeftAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_team_players", x => x.Id);
                    table.ForeignKey(
                        name: "FK_team_players_players_PlayerId",
                        column: x => x.PlayerId,
                        principalSchema: "players",
                        principalTable: "players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_team_players_PlayerId",
                schema: "players",
                table: "team_players",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_team_players_TeamId_PlayerId",
                schema: "players",
                table: "team_players",
                columns: new[] { "TeamId", "PlayerId" },
                unique: true);

            // Kadry z dotychczasowych składów: każdy, kto grał w zgłoszeniu drużyny, trafia do jej kadry.
            // Numer z najnowszego składu. Kto był wszędzie usunięty ze składu, wchodzi jako były zawodnik.
            // Tabela zgłoszeń należy do modułu Teams; na pustej bazie jej migracja mogła jeszcze nie przejść.
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM information_schema.tables
                        WHERE table_schema = 'teams' AND table_name = 'tournament_teams'
                    ) THEN
                        INSERT INTO players.team_players
                            ("Id", "TeamId", "PlayerId", "ShirtNumber", "Status", "LeftAt", "CreatedAt", "UpdatedAt")
                        SELECT
                            gen_random_uuid(),
                            tt."TeamId",
                            tp."PlayerId",
                            (array_agg(tp."ShirtNumber" ORDER BY tp."CreatedAt" DESC)
                                FILTER (WHERE tp."ShirtNumber" IS NOT NULL))[1],
                            CASE WHEN bool_or(tp."Status" <> 4) THEN 1 ELSE 2 END,
                            CASE WHEN bool_or(tp."Status" <> 4) THEN NULL ELSE now() END,
                            min(tp."CreatedAt"),
                            now()
                        FROM players.tournament_players tp
                        JOIN teams.tournament_teams tt ON tt."Id" = tp."TournamentTeamId"
                        GROUP BY tt."TeamId", tp."PlayerId"
                        ON CONFLICT ("TeamId", "PlayerId") DO NOTHING;
                    END IF;
                END $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "team_players",
                schema: "players");
        }
    }
}
