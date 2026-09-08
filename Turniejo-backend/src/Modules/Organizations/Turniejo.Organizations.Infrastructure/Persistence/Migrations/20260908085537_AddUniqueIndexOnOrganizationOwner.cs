using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Turniejo.Organizations.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUniqueIndexOnOrganizationOwner : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_organizations_owner",
                schema: "organizations",
                table: "organizations");

            migrationBuilder.CreateIndex(
                name: "IX_organizations_owner",
                schema: "organizations",
                table: "organizations",
                column: "OwnerUserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_organizations_owner",
                schema: "organizations",
                table: "organizations");

            migrationBuilder.CreateIndex(
                name: "IX_organizations_owner",
                schema: "organizations",
                table: "organizations",
                column: "OwnerUserId");
        }
    }
}
