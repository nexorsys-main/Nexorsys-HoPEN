using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nexorsys.Identity.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UniqueWorkstationCertificateBinding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_workstations_device_certificate_thumbprint",
                table: "workstations",
                column: "device_certificate_thumbprint",
                unique: true,
                filter: "device_certificate_thumbprint IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_workstations_device_certificate_thumbprint",
                table: "workstations");
        }
    }
}
