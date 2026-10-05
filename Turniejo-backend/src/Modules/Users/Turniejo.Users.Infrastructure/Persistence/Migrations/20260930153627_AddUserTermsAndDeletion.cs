using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Turniejo.Users.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUserTermsAndDeletion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedAt",
                schema: "users",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "TermsAcceptedAt",
                schema: "users",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TermsVersion",
                schema: "users",
                table: "users",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "users",
                table: "users");

            migrationBuilder.DropColumn(
                name: "TermsAcceptedAt",
                schema: "users",
                table: "users");

            migrationBuilder.DropColumn(
                name: "TermsVersion",
                schema: "users",
                table: "users");
        }
    }
}
