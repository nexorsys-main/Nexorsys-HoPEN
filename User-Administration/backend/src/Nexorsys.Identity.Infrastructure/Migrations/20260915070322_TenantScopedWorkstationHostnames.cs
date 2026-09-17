using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nexorsys.Identity.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class TenantScopedWorkstationHostnames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_workstations_hostname",
                table: "workstations");

            migrationBuilder.CreateIndex(
                name: "ix_workstations_organization_id_hostname",
                table: "workstations",
                columns: new[] { "organization_id", "hostname" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_workstations_organization_id_hostname",
                table: "workstations");

            migrationBuilder.CreateIndex(
                name: "ix_workstations_hostname",
                table: "workstations",
                column: "hostname",
                unique: true);
        }
    }
}
