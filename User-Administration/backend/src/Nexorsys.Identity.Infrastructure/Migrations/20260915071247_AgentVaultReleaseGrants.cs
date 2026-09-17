using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nexorsys.Identity.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AgentVaultReleaseGrants : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "agent_certificate_thumbprint",
                table: "workstations",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "agent_certificate_validated_at",
                table: "workstations",
                type: "timestamp",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "vault_release_grants",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    workstation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    application_id = table.Column<Guid>(type: "uuid", nullable: false),
                    application_session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    vault_entry_id = table.Column<Guid>(type: "uuid", nullable: false),
                    agent_certificate_thumbprint = table.Column<string>(type: "text", nullable: false),
                    token_hash = table.Column<byte[]>(type: "bytea", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp", nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp", nullable: false),
                    consumed_at = table.Column<DateTime>(type: "timestamp", nullable: true),
                    revoked_at = table.Column<DateTime>(type: "timestamp", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_vault_release_grants", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_workstations_agent_certificate_thumbprint",
                table: "workstations",
                column: "agent_certificate_thumbprint",
                unique: true,
                filter: "agent_certificate_thumbprint IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_vault_release_grants_organization_id_workstation_id_expires",
                table: "vault_release_grants",
                columns: new[] { "organization_id", "workstation_id", "expires_at" });

            migrationBuilder.CreateIndex(
                name: "ix_vault_release_grants_token_hash",
                table: "vault_release_grants",
                column: "token_hash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "vault_release_grants");

            migrationBuilder.DropIndex(
                name: "ix_workstations_agent_certificate_thumbprint",
                table: "workstations");

            migrationBuilder.DropColumn(
                name: "agent_certificate_thumbprint",
                table: "workstations");

            migrationBuilder.DropColumn(
                name: "agent_certificate_validated_at",
                table: "workstations");
        }
    }
}
