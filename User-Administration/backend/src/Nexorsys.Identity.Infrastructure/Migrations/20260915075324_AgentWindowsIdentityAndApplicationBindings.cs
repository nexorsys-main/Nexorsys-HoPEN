using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nexorsys.Identity.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AgentWindowsIdentityAndApplicationBindings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "windows_binding_id",
                table: "vault_release_grants",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "windows_session_id",
                table: "vault_release_grants",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "agent_registration_updated_at",
                table: "applications",
                type: "timestamp",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "agent_registration_updated_by",
                table: "applications",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "approved_install_root",
                table: "applications",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "credential_delivery_mechanism",
                table: "applications",
                type: "text",
                nullable: false,
                defaultValue: "none");

            migrationBuilder.AddColumn<string>(
                name: "expected_publisher_thumbprint",
                table: "applications",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "application_id",
                table: "application_sessions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "user_session_id",
                table: "application_sessions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "agent_windows_session_bindings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    workstation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    windows_user_identity_binding_id = table.Column<Guid>(type: "uuid", nullable: false),
                    windows_sid = table.Column<string>(type: "character varying(184)", maxLength: 184, nullable: false),
                    windows_session_id = table.Column<int>(type: "integer", nullable: false),
                    agent_certificate_thumbprint = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp", nullable: false),
                    last_validated_at = table.Column<DateTime>(type: "timestamp", nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp", nullable: false),
                    revoked_at = table.Column<DateTime>(type: "timestamp", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_agent_windows_session_bindings", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "windows_user_identity_bindings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    workstation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    windows_sid = table.Column<string>(type: "character varying(184)", maxLength: 184, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    revoked_at = table.Column<DateTime>(type: "timestamp", nullable: true),
                    revoked_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_windows_user_identity_bindings", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_application_sessions_application_id",
                table: "application_sessions",
                column: "application_id");

            migrationBuilder.CreateIndex(
                name: "ix_application_sessions_user_session_id",
                table: "application_sessions",
                column: "user_session_id");

            migrationBuilder.CreateIndex(
                name: "ix_agent_windows_session_bindings_organization_id_user_session",
                table: "agent_windows_session_bindings",
                columns: new[] { "organization_id", "user_session_id", "expires_at" });

            migrationBuilder.CreateIndex(
                name: "ix_agent_windows_session_bindings_organization_id_workstation_",
                table: "agent_windows_session_bindings",
                columns: new[] { "organization_id", "workstation_id", "windows_session_id" },
                unique: true,
                filter: "revoked_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_windows_user_identity_bindings_organization_id_workstation_",
                table: "windows_user_identity_bindings",
                columns: new[] { "organization_id", "workstation_id", "windows_sid" },
                unique: true,
                filter: "is_active = true");

            migrationBuilder.AddForeignKey(
                name: "fk_application_sessions_applications_application_id",
                table: "application_sessions",
                column: "application_id",
                principalTable: "applications",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_application_sessions_user_sessions_user_session_id",
                table: "application_sessions",
                column: "user_session_id",
                principalTable: "user_sessions",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_application_sessions_applications_application_id",
                table: "application_sessions");

            migrationBuilder.DropForeignKey(
                name: "fk_application_sessions_user_sessions_user_session_id",
                table: "application_sessions");

            migrationBuilder.DropTable(
                name: "agent_windows_session_bindings");

            migrationBuilder.DropTable(
                name: "windows_user_identity_bindings");

            migrationBuilder.DropIndex(
                name: "ix_application_sessions_application_id",
                table: "application_sessions");

            migrationBuilder.DropIndex(
                name: "ix_application_sessions_user_session_id",
                table: "application_sessions");

            migrationBuilder.DropColumn(
                name: "windows_binding_id",
                table: "vault_release_grants");

            migrationBuilder.DropColumn(
                name: "windows_session_id",
                table: "vault_release_grants");

            migrationBuilder.DropColumn(
                name: "agent_registration_updated_at",
                table: "applications");

            migrationBuilder.DropColumn(
                name: "agent_registration_updated_by",
                table: "applications");

            migrationBuilder.DropColumn(
                name: "approved_install_root",
                table: "applications");

            migrationBuilder.DropColumn(
                name: "credential_delivery_mechanism",
                table: "applications");

            migrationBuilder.DropColumn(
                name: "expected_publisher_thumbprint",
                table: "applications");

            migrationBuilder.DropColumn(
                name: "application_id",
                table: "application_sessions");

            migrationBuilder.DropColumn(
                name: "user_session_id",
                table: "application_sessions");
        }
    }
}
