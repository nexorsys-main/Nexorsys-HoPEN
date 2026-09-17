using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nexorsys.Identity.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class LicenseActivationState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_trial",
                table: "organization_licenses",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "license_id",
                table: "organization_licenses",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "not_before",
                table: "organization_licenses",
                type: "timestamp",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "is_trial",
                table: "organization_licenses");

            migrationBuilder.DropColumn(
                name: "license_id",
                table: "organization_licenses");

            migrationBuilder.DropColumn(
                name: "not_before",
                table: "organization_licenses");
        }
    }
}
