using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nexorsys.Identity.Infrastructure.Migrations
{
	/// <inheritdoc />
	public partial class Phase1_Foundation : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.AddColumn<bool>(
				name: "credential_provider_enabled",
				table: "users",
				type: "boolean",
				nullable: false,
				defaultValue: false);

			migrationBuilder.AddColumn<DateTime>(
				name: "enrollment_expiration_date",
				table: "users",
				type: "timestamp",
				nullable: true);

			migrationBuilder.AddColumn<string>(
				name: "enrollment_status",
				table: "users",
				type: "text",
				nullable: true);

			migrationBuilder.AddColumn<string>(
				name: "enrollment_workstation",
				table: "users",
				type: "text",
				nullable: true);

			migrationBuilder.AddColumn<DateTime>(
				name: "last_credential_refresh",
				table: "users",
				type: "timestamp",
				nullable: true);

			migrationBuilder.AddColumn<DateTime>(
				name: "last_windows_login",
				table: "users",
				type: "timestamp",
				nullable: true);

			migrationBuilder.AddColumn<bool>(
				name: "requires_re_enrollment",
				table: "users",
				type: "boolean",
				nullable: false,
				defaultValue: false);

			migrationBuilder.AddColumn<bool>(
				name: "windows_auth_enabled",
				table: "users",
				type: "boolean",
				nullable: false,
				defaultValue: false);

			migrationBuilder.AddColumn<string>(
				name: "windows_auth_mode",
				table: "users",
				type: "text",
				nullable: true);

			migrationBuilder.CreateTable(
				name: "department_policies",
				columns: table => new
				{
					id = table.Column<string>(type: "text", nullable: false),
					allowed_authentication_methods = table.Column<string>(type: "text", nullable: true),
					required_assurance_level = table.Column<string>(type: "text", nullable: true),
					authorized_workstations = table.Column<string>(type: "text", nullable: true),
					session_timeout_minutes = table.Column<int>(type: "integer", nullable: false)
				},
				constraints: table =>
				{
					table.PrimaryKey("pk_department_policies", x => x.id);
				});

			migrationBuilder.CreateTable(
				name: "feature_flags",
				columns: table => new
				{
					id = table.Column<string>(type: "text", nullable: false),
					is_enabled = table.Column<bool>(type: "boolean", nullable: false),
					description = table.Column<string>(type: "text", nullable: true),
					updated_at = table.Column<DateTime>(type: "timestamp", nullable: false)
				},
				constraints: table =>
				{
					table.PrimaryKey("pk_feature_flags", x => x.id);
				});

			migrationBuilder.CreateTable(
				name: "organization_licenses",
				columns: table => new
				{
					id = table.Column<Guid>(type: "uuid", nullable: false),
					licensed_users = table.Column<int>(type: "integer", nullable: false),
					licensed_workstations = table.Column<int>(type: "integer", nullable: false),
					licensed_modules = table.Column<string>(type: "text", nullable: true),
					expiry_date = table.Column<DateTime>(type: "timestamp", nullable: false),
					support_level = table.Column<string>(type: "text", nullable: true)
				},
				constraints: table =>
				{
					table.PrimaryKey("pk_organization_licenses", x => x.id);
				});

			migrationBuilder.CreateTable(
				name: "workstations",
				columns: table => new
				{
					id = table.Column<Guid>(type: "uuid", nullable: false),
					hostname = table.Column<string>(type: "text", nullable: false),
					department = table.Column<string>(type: "text", nullable: true),
					ip_address = table.Column<string>(type: "text", nullable: true),
					location = table.Column<string>(type: "text", nullable: true),
					is_active = table.Column<bool>(type: "boolean", nullable: false),
					windows_auth_enabled = table.Column<bool>(type: "boolean", nullable: false),
					last_seen_at = table.Column<DateTime>(type: "timestamp", nullable: true),
					created_at = table.Column<DateTime>(type: "timestamp", nullable: false),
					device_certificate_thumbprint = table.Column<string>(type: "text", nullable: true)
				},
				constraints: table =>
				{
					table.PrimaryKey("pk_workstations", x => x.id);
				});

			migrationBuilder.CreateTable(
				name: "authentication_events",
				columns: table => new
				{
					id = table.Column<Guid>(type: "uuid", nullable: false),
					timestamp = table.Column<DateTime>(type: "timestamp", nullable: false),
					user_id = table.Column<Guid>(type: "uuid", nullable: true),
					provider_type = table.Column<string>(type: "text", nullable: false),
					workstation_id = table.Column<Guid>(type: "uuid", nullable: true),
					department = table.Column<string>(type: "text", nullable: true),
					event_type = table.Column<string>(type: "text", nullable: false),
					result = table.Column<string>(type: "text", nullable: false),
					risk_level = table.Column<string>(type: "text", nullable: true),
					ip_address = table.Column<string>(type: "text", nullable: true)
				},
				constraints: table =>
				{
					table.PrimaryKey("pk_authentication_events", x => x.id);
					table.ForeignKey(
						name: "fk_authentication_events_users_user_id",
						column: x => x.user_id,
						principalTable: "users",
						principalColumn: "id",
						onDelete: ReferentialAction.SetNull);
					table.ForeignKey(
						name: "fk_authentication_events_workstations_workstation_id",
						column: x => x.workstation_id,
						principalTable: "workstations",
						principalColumn: "id",
						onDelete: ReferentialAction.SetNull);
				});

			migrationBuilder.CreateTable(
				name: "credential_provider_fleets",
				columns: table => new
				{
					id = table.Column<Guid>(type: "uuid", nullable: false),
					workstation_id = table.Column<Guid>(type: "uuid", nullable: false),
					installed_version = table.Column<string>(type: "text", nullable: true),
					last_check_in_at = table.Column<DateTime>(type: "timestamp", nullable: true),
					provider_status = table.Column<string>(type: "text", nullable: false),
					windows_version = table.Column<string>(type: "text", nullable: true),
					enrollment_count = table.Column<int>(type: "integer", nullable: false)
				},
				constraints: table =>
				{
					table.PrimaryKey("pk_credential_provider_fleets", x => x.id);
					table.ForeignKey(
						name: "fk_credential_provider_fleets_workstations_workstation_id",
						column: x => x.workstation_id,
						principalTable: "workstations",
						principalColumn: "id",
						onDelete: ReferentialAction.Cascade);
				});

			migrationBuilder.CreateTable(
				name: "user_sessions",
				columns: table => new
				{
					id = table.Column<Guid>(type: "uuid", nullable: false),
					user_id = table.Column<Guid>(type: "uuid", nullable: false),
					workstation_id = table.Column<Guid>(type: "uuid", nullable: true),
					department = table.Column<string>(type: "text", nullable: true),
					session_started_at = table.Column<DateTime>(type: "timestamp", nullable: false),
					session_ended_at = table.Column<DateTime>(type: "timestamp", nullable: true),
					duration_minutes = table.Column<int>(type: "integer", nullable: true)
				},
				constraints: table =>
				{
					table.PrimaryKey("pk_user_sessions", x => x.id);
					table.ForeignKey(
						name: "fk_user_sessions_users_user_id",
						column: x => x.user_id,
						principalTable: "users",
						principalColumn: "id",
						onDelete: ReferentialAction.Cascade);
					table.ForeignKey(
						name: "fk_user_sessions_workstations_workstation_id",
						column: x => x.workstation_id,
						principalTable: "workstations",
						principalColumn: "id",
						onDelete: ReferentialAction.SetNull);
				});

			migrationBuilder.CreateTable(
				name: "user_workstations",
				columns: table => new
				{
					user_id = table.Column<Guid>(type: "uuid", nullable: false),
					workstation_id = table.Column<Guid>(type: "uuid", nullable: false)
				},
				constraints: table =>
				{
					table.PrimaryKey("pk_user_workstations", x => new { x.user_id, x.workstation_id });
					table.ForeignKey(
						name: "fk_user_workstations_users_user_id",
						column: x => x.user_id,
						principalTable: "users",
						principalColumn: "id",
						onDelete: ReferentialAction.Cascade);
					table.ForeignKey(
						name: "fk_user_workstations_workstations_workstation_id",
						column: x => x.workstation_id,
						principalTable: "workstations",
						principalColumn: "id",
						onDelete: ReferentialAction.Cascade);
				});

			migrationBuilder.CreateIndex(
				name: "ix_authentication_events_user_id",
				table: "authentication_events",
				column: "user_id");

			migrationBuilder.CreateIndex(
				name: "ix_authentication_events_workstation_id",
				table: "authentication_events",
				column: "workstation_id");

			migrationBuilder.CreateIndex(
				name: "ix_credential_provider_fleets_workstation_id",
				table: "credential_provider_fleets",
				column: "workstation_id",
				unique: true);

			migrationBuilder.CreateIndex(
				name: "ix_user_sessions_user_id",
				table: "user_sessions",
				column: "user_id");

			migrationBuilder.CreateIndex(
				name: "ix_user_sessions_workstation_id",
				table: "user_sessions",
				column: "workstation_id");

			migrationBuilder.CreateIndex(
				name: "ix_user_workstations_workstation_id",
				table: "user_workstations",
				column: "workstation_id");

			migrationBuilder.CreateIndex(
				name: "ix_workstations_hostname",
				table: "workstations",
				column: "hostname",
				unique: true);
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropTable(
				name: "authentication_events");

			migrationBuilder.DropTable(
				name: "credential_provider_fleets");

			migrationBuilder.DropTable(
				name: "department_policies");

			migrationBuilder.DropTable(
				name: "feature_flags");

			migrationBuilder.DropTable(
				name: "organization_licenses");

			migrationBuilder.DropTable(
				name: "user_sessions");

			migrationBuilder.DropTable(
				name: "user_workstations");

			migrationBuilder.DropTable(
				name: "workstations");

			migrationBuilder.DropColumn(
				name: "credential_provider_enabled",
				table: "users");

			migrationBuilder.DropColumn(
				name: "enrollment_expiration_date",
				table: "users");

			migrationBuilder.DropColumn(
				name: "enrollment_status",
				table: "users");

			migrationBuilder.DropColumn(
				name: "enrollment_workstation",
				table: "users");

			migrationBuilder.DropColumn(
				name: "last_credential_refresh",
				table: "users");

			migrationBuilder.DropColumn(
				name: "last_windows_login",
				table: "users");

			migrationBuilder.DropColumn(
				name: "requires_re_enrollment",
				table: "users");

			migrationBuilder.DropColumn(
				name: "windows_auth_enabled",
				table: "users");

			migrationBuilder.DropColumn(
				name: "windows_auth_mode",
				table: "users");
		}
	}
}
