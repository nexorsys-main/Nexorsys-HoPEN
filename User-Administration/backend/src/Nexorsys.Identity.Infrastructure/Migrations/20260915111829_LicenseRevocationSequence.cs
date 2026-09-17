using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nexorsys.Identity.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class LicenseRevocationSequence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "highest_revocation_sequence",
                table: "organization_licenses",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "highest_revocation_sequence",
                table: "organization_licenses");
        }
    }
}
