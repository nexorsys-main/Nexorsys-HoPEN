using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nexorsys.Identity.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EnforceUniqueAgentCertificateBinding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM workstations
                        WHERE agent_certificate_thumbprint IS NOT NULL
                        GROUP BY agent_certificate_thumbprint
                        HAVING COUNT(*) > 1
                    ) THEN
                        RAISE EXCEPTION 'Cannot enforce unique Agent certificate binding: duplicate agent_certificate_thumbprint values exist. Resolve duplicate workstation bindings before retrying this migration.';
                    END IF;
                END $$;
                """);

            migrationBuilder.DropIndex(
                name: "ix_workstations_agent_certificate_thumbprint",
                table: "workstations");

            migrationBuilder.CreateIndex(
                name: "ix_workstations_agent_certificate_thumbprint",
                table: "workstations",
                column: "agent_certificate_thumbprint",
                unique: true,
                filter: "agent_certificate_thumbprint IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_workstations_agent_certificate_thumbprint",
                table: "workstations");

            migrationBuilder.CreateIndex(
                name: "ix_workstations_agent_certificate_thumbprint",
                table: "workstations",
                column: "agent_certificate_thumbprint",
                filter: "agent_certificate_thumbprint IS NOT NULL");
        }
    }
}
