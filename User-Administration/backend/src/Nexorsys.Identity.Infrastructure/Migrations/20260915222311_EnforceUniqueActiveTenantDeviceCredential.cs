using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nexorsys.Identity.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EnforceUniqueActiveTenantDeviceCredential : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM user_devices
                        WHERE status <> 'revoked' AND device_identifier IS NOT NULL
                        GROUP BY organization_id, device_type, device_identifier
                        HAVING COUNT(*) > 1
                    ) THEN
                        RAISE EXCEPTION 'Cannot enforce unique active MIE credentials: duplicate active device bindings exist within an organization. Resolve duplicate bindings before retrying this migration.';
                    END IF;
                END $$;
                """);

            migrationBuilder.CreateIndex(
                name: "ux_user_devices_org_type_identifier_active",
                table: "user_devices",
                columns: new[] { "organization_id", "device_type", "device_identifier" },
                unique: true,
                filter: "status <> 'revoked'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_user_devices_org_type_identifier_active",
                table: "user_devices");
        }
    }
}
