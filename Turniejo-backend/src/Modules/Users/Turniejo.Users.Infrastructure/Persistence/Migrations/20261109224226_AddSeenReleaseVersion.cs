using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Turniejo.Users.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSeenReleaseVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SeenReleaseVersion",
                schema: "users",
                table: "users",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            // Konta sprzed okna "Co nowego" znają już 1.0.0 (ten wpis streszcza całą aplikację), więc
            // pierwsze okno zobaczą przy następnym wydaniu. Nowe konta zostają z null.
            migrationBuilder.Sql("UPDATE users.users SET \"SeenReleaseVersion\" = '1.0.0';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SeenReleaseVersion",
                schema: "users",
                table: "users");
        }
    }
}
