using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nexorsys.Identity.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CommercialTenantSecurityFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "authentication_mode",
                table: "workstations",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "machine_sid",
                table: "workstations",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "organization_id",
                table: "workstations",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "organization_id",
                table: "workstation_sessions",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "organization_id",
                table: "workflows",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "organization_id",
                table: "users",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "organization_id",
                table: "user_workstations",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "organization_id",
                table: "user_sessions",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "organization_id",
                table: "user_pins",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "organization_id",
                table: "user_permissions",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "organization_id",
                table: "user_devices",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "organization_id",
                table: "session_events",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "organization_id",
                table: "pin_reset_requests",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "organization_id",
                table: "organization_licenses",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "organization_id",
                table: "migration_phases",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "organization_id",
                table: "kiosk_sessions",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "organization_id",
                table: "identity_providers",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "organization_id",
                table: "federation_tokens",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "organization_id",
                table: "feature_flags",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "organization_id",
                table: "department_policies",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "organization_id",
                table: "credential_provider_fleets",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "organization_id",
                table: "authentication_policies",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "organization_id",
                table: "authentication_events",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "organization_id",
                table: "audit_logs",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "organization_id",
                table: "applications",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "organization_id",
                table: "ans_delegation_logs",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "application_sessions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    application_name = table.Column<string>(type: "text", nullable: false),
                    start_time = table.Column<DateTime>(type: "timestamp", nullable: false),
                    end_time = table.Column<DateTime>(type: "timestamp", nullable: true),
                    workstation_id = table.Column<Guid>(type: "uuid", nullable: true),
                    department = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_application_sessions", x => x.id);
                    table.ForeignKey(
                        name: "fk_application_sessions_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_application_sessions_workstations_workstation_id",
                        column: x => x.workstation_id,
                        principalTable: "workstations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "group_role_memberships",
                columns: table => new
                {
                    group_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role_id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_group_role_memberships", x => new { x.group_id, x.role_id });
                });

            migrationBuilder.CreateTable(
                name: "organizations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    slug = table.Column<string>(type: "text", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_organizations", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "permission_definitions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    key = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_permission_definitions", x => x.id);
                });

            migrationBuilder.Sql(@"
                INSERT INTO organizations (id, name, slug, is_active, created_at, updated_at)
                VALUES ('00000000-0000-0000-0000-000000000001', 'Migrated Organization', 'migrated-organization', true, NOW(), NOW())
                ON CONFLICT (id) DO NOTHING;
                UPDATE users SET organization_id = '00000000-0000-0000-0000-000000000001' WHERE organization_id = '00000000-0000-0000-0000-000000000000';
                UPDATE user_pins SET organization_id = '00000000-0000-0000-0000-000000000001' WHERE organization_id = '00000000-0000-0000-0000-000000000000';
                UPDATE user_permissions SET organization_id = '00000000-0000-0000-0000-000000000001' WHERE organization_id = '00000000-0000-0000-0000-000000000000';
                UPDATE applications SET organization_id = '00000000-0000-0000-0000-000000000001' WHERE organization_id = '00000000-0000-0000-0000-000000000000';
                UPDATE workstations SET organization_id = '00000000-0000-0000-0000-000000000001' WHERE organization_id = '00000000-0000-0000-0000-000000000000';
                UPDATE audit_logs SET organization_id = '00000000-0000-0000-0000-000000000001' WHERE organization_id = '00000000-0000-0000-0000-000000000000';
                UPDATE authentication_events SET organization_id = '00000000-0000-0000-0000-000000000001' WHERE organization_id = '00000000-0000-0000-0000-000000000000';
                UPDATE kiosk_sessions SET organization_id = '00000000-0000-0000-0000-000000000001' WHERE organization_id = '00000000-0000-0000-0000-000000000000';
                UPDATE user_sessions SET organization_id = '00000000-0000-0000-0000-000000000001' WHERE organization_id = '00000000-0000-0000-0000-000000000000';
                UPDATE workstation_sessions SET organization_id = '00000000-0000-0000-0000-000000000001' WHERE organization_id = '00000000-0000-0000-0000-000000000000';
                UPDATE user_devices SET organization_id = '00000000-0000-0000-0000-000000000001' WHERE organization_id = '00000000-0000-0000-0000-000000000000';
                UPDATE workflows SET organization_id = '00000000-0000-0000-0000-000000000001' WHERE organization_id = '00000000-0000-0000-0000-000000000000';
                UPDATE identity_providers SET organization_id = '00000000-0000-0000-0000-000000000001' WHERE organization_id = '00000000-0000-0000-0000-000000000000';
                UPDATE authentication_policies SET organization_id = '00000000-0000-0000-0000-000000000001' WHERE organization_id = '00000000-0000-0000-0000-000000000000';
                UPDATE federation_tokens SET organization_id = '00000000-0000-0000-0000-000000000001' WHERE organization_id = '00000000-0000-0000-0000-000000000000';
                UPDATE migration_phases SET organization_id = '00000000-0000-0000-0000-000000000001' WHERE organization_id = '00000000-0000-0000-0000-000000000000';
                UPDATE organization_licenses SET organization_id = '00000000-0000-0000-0000-000000000001' WHERE organization_id = '00000000-0000-0000-0000-000000000000';
                UPDATE feature_flags SET organization_id = '00000000-0000-0000-0000-000000000001' WHERE organization_id = '00000000-0000-0000-0000-000000000000';
                UPDATE department_policies SET organization_id = '00000000-0000-0000-0000-000000000001' WHERE organization_id = '00000000-0000-0000-0000-000000000000';
                UPDATE credential_provider_fleets SET organization_id = '00000000-0000-0000-0000-000000000001' WHERE organization_id = '00000000-0000-0000-0000-000000000000';
                UPDATE ans_delegation_logs SET organization_id = '00000000-0000-0000-0000-000000000001' WHERE organization_id = '00000000-0000-0000-0000-000000000000';
                UPDATE pin_reset_requests SET organization_id = '00000000-0000-0000-0000-000000000001' WHERE organization_id = '00000000-0000-0000-0000-000000000000';
                UPDATE session_events SET organization_id = '00000000-0000-0000-0000-000000000001' WHERE organization_id = '00000000-0000-0000-0000-000000000000';
            ");

            migrationBuilder.CreateTable(
                name: "role_definitions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    is_system_role = table.Column<bool>(type: "boolean", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_role_definitions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "role_permissions",
                columns: table => new
                {
                    role_id = table.Column<Guid>(type: "uuid", nullable: false),
                    permission_id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_role_permissions", x => new { x.role_id, x.permission_id });
                });

            migrationBuilder.CreateTable(
                name: "security_groups",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_security_groups", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "sites",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    code = table.Column<string>(type: "text", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sites", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "user_group_memberships",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    group_id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_group_memberships", x => new { x.user_id, x.group_id });
                });

            migrationBuilder.CreateTable(
                name: "user_role_memberships",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role_id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_role_memberships", x => new { x.user_id, x.role_id });
                });

            migrationBuilder.CreateTable(
                name: "vault_entries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    application_id = table.Column<Guid>(type: "uuid", nullable: false),
                    username = table.Column<string>(type: "text", nullable: false),
                    encrypted_secret = table.Column<byte[]>(type: "bytea", nullable: false),
                    secret_version = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp", nullable: false),
                    last_used_at = table.Column<DateTime>(type: "timestamp", nullable: true),
                    expires_at = table.Column<DateTime>(type: "timestamp", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_vault_entries", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_application_sessions_user_id_application_name",
                table: "application_sessions",
                columns: new[] { "user_id", "application_name" });

            migrationBuilder.CreateIndex(
                name: "ix_application_sessions_workstation_id",
                table: "application_sessions",
                column: "workstation_id");

            migrationBuilder.CreateIndex(
                name: "ix_organizations_slug",
                table: "organizations",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_permission_definitions_key",
                table: "permission_definitions",
                column: "key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_role_definitions_organization_id_name",
                table: "role_definitions",
                columns: new[] { "organization_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_security_groups_organization_id_name",
                table: "security_groups",
                columns: new[] { "organization_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sites_organization_id_name",
                table: "sites",
                columns: new[] { "organization_id", "name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "application_sessions");

            migrationBuilder.DropTable(
                name: "group_role_memberships");

            migrationBuilder.DropTable(
                name: "organizations");

            migrationBuilder.DropTable(
                name: "permission_definitions");

            migrationBuilder.DropTable(
                name: "role_definitions");

            migrationBuilder.DropTable(
                name: "role_permissions");

            migrationBuilder.DropTable(
                name: "security_groups");

            migrationBuilder.DropTable(
                name: "sites");

            migrationBuilder.DropTable(
                name: "user_group_memberships");

            migrationBuilder.DropTable(
                name: "user_role_memberships");

            migrationBuilder.DropTable(
                name: "vault_entries");

            migrationBuilder.DropColumn(
                name: "authentication_mode",
                table: "workstations");

            migrationBuilder.DropColumn(
                name: "machine_sid",
                table: "workstations");

            migrationBuilder.DropColumn(
                name: "organization_id",
                table: "workstations");

            migrationBuilder.DropColumn(
                name: "organization_id",
                table: "workstation_sessions");

            migrationBuilder.DropColumn(
                name: "organization_id",
                table: "workflows");

            migrationBuilder.DropColumn(
                name: "organization_id",
                table: "users");

            migrationBuilder.DropColumn(
                name: "organization_id",
                table: "user_workstations");

            migrationBuilder.DropColumn(
                name: "organization_id",
                table: "user_sessions");

            migrationBuilder.DropColumn(
                name: "organization_id",
                table: "user_pins");

            migrationBuilder.DropColumn(
                name: "organization_id",
                table: "user_permissions");

            migrationBuilder.DropColumn(
                name: "organization_id",
                table: "user_devices");

            migrationBuilder.DropColumn(
                name: "organization_id",
                table: "session_events");

            migrationBuilder.DropColumn(
                name: "organization_id",
                table: "pin_reset_requests");

            migrationBuilder.DropColumn(
                name: "organization_id",
                table: "organization_licenses");

            migrationBuilder.DropColumn(
                name: "organization_id",
                table: "migration_phases");

            migrationBuilder.DropColumn(
                name: "organization_id",
                table: "kiosk_sessions");

            migrationBuilder.DropColumn(
                name: "organization_id",
                table: "identity_providers");

            migrationBuilder.DropColumn(
                name: "organization_id",
                table: "federation_tokens");

            migrationBuilder.DropColumn(
                name: "organization_id",
                table: "feature_flags");

            migrationBuilder.DropColumn(
                name: "organization_id",
                table: "department_policies");

            migrationBuilder.DropColumn(
                name: "organization_id",
                table: "credential_provider_fleets");

            migrationBuilder.DropColumn(
                name: "organization_id",
                table: "authentication_policies");

            migrationBuilder.DropColumn(
                name: "organization_id",
                table: "authentication_events");

            migrationBuilder.DropColumn(
                name: "organization_id",
                table: "audit_logs");

            migrationBuilder.DropColumn(
                name: "organization_id",
                table: "applications");

            migrationBuilder.DropColumn(
                name: "organization_id",
                table: "ans_delegation_logs");
        }
    }
}
