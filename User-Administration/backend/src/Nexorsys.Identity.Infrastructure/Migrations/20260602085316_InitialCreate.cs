using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nexorsys.Identity.Infrastructure.Migrations
{
	/// <inheritdoc />
	public partial class InitialCreate : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.CreateTable(
				name: "applications",
				columns: table => new
				{
					id = table.Column<Guid>(type: "uuid", nullable: false),
					name = table.Column<string>(type: "text", nullable: false),
					description = table.Column<string>(type: "text", nullable: true),
					client_id = table.Column<string>(type: "text", nullable: false),
					client_secret = table.Column<string>(type: "text", nullable: true),
					redirect_uris = table.Column<string[]>(type: "text[]", nullable: false),
					is_active = table.Column<bool>(type: "boolean", nullable: false),
					created_at = table.Column<DateTime>(type: "timestamp", nullable: false)
				},
				constraints: table =>
				{
					table.PrimaryKey("pk_applications", x => x.id);
				});

			migrationBuilder.CreateTable(
				name: "identity_providers",
				columns: table => new
				{
					id = table.Column<Guid>(type: "uuid", nullable: false),
					provider_name = table.Column<string>(type: "text", nullable: false),
					provider_type = table.Column<string>(type: "text", nullable: false),
					is_enabled = table.Column<bool>(type: "boolean", nullable: false),
					priority = table.Column<int>(type: "integer", nullable: false),
					configuration_json = table.Column<string>(type: "jsonb", nullable: true),
					health_status = table.Column<string>(type: "text", nullable: false),
					last_health_check = table.Column<DateTime>(type: "timestamp", nullable: true),
					created_at = table.Column<DateTime>(type: "timestamp", nullable: false),
					updated_at = table.Column<DateTime>(type: "timestamp", nullable: false)
				},
				constraints: table =>
				{
					table.PrimaryKey("pk_identity_providers", x => x.id);
				});

			migrationBuilder.CreateTable(
				name: "migration_phases",
				columns: table => new
				{
					id = table.Column<Guid>(type: "uuid", nullable: false),
					phase_name = table.Column<string>(type: "text", nullable: false),
					description = table.Column<string>(type: "text", nullable: true),
					active_providers_json = table.Column<string>(type: "jsonb", nullable: true),
					federation_enabled = table.Column<bool>(type: "boolean", nullable: false),
					is_current_phase = table.Column<bool>(type: "boolean", nullable: false),
					activated_at = table.Column<DateTime>(type: "timestamp", nullable: true),
					created_at = table.Column<DateTime>(type: "timestamp", nullable: false)
				},
				constraints: table =>
				{
					table.PrimaryKey("pk_migration_phases", x => x.id);
				});

			migrationBuilder.CreateTable(
				name: "users",
				columns: table => new
				{
					id = table.Column<Guid>(type: "uuid", nullable: false),
					ad_guid = table.Column<string>(type: "text", nullable: true),
					sam_account_name = table.Column<string>(type: "text", nullable: false),
					distinguished_name = table.Column<string>(type: "text", nullable: false),
					display_name = table.Column<string>(type: "text", nullable: true),
					first_name = table.Column<string>(type: "text", nullable: true),
					last_name = table.Column<string>(type: "text", nullable: true),
					email = table.Column<string>(type: "text", nullable: true),
					phone_number = table.Column<string>(type: "text", nullable: true),
					password_hash = table.Column<string>(type: "text", nullable: true),
					department = table.Column<string>(type: "text", nullable: true),
					title = table.Column<string>(type: "text", nullable: true),
					manager_ad_guid = table.Column<string>(type: "text", nullable: true),
					employee_id = table.Column<string>(type: "text", nullable: true),
					badge_uid = table.Column<string>(type: "text", nullable: true),
					cps_id = table.Column<string>(type: "text", nullable: true),
					fido_id = table.Column<string>(type: "text", nullable: true),
					badge_type = table.Column<string>(type: "text", nullable: true),
					is_active = table.Column<bool>(type: "boolean", nullable: false),
					is_local_profile = table.Column<bool>(type: "boolean", nullable: false),
					rpps_number = table.Column<string>(type: "text", nullable: true),
					role = table.Column<string>(type: "text", nullable: false),
					must_change_pin = table.Column<bool>(type: "boolean", nullable: false),
					failed_pin_attempts = table.Column<int>(type: "integer", nullable: false),
					locked_until = table.Column<DateTime>(type: "timestamp", nullable: true),
					last_login_at = table.Column<DateTime>(type: "timestamp", nullable: true),
					created_at = table.Column<DateTime>(type: "timestamp", nullable: false),
					updated_at = table.Column<DateTime>(type: "timestamp", nullable: false)
				},
				constraints: table =>
				{
					table.PrimaryKey("pk_users", x => x.id);
				});

			migrationBuilder.CreateTable(
				name: "authentication_policies",
				columns: table => new
				{
					id = table.Column<Guid>(type: "uuid", nullable: false),
					policy_name = table.Column<string>(type: "text", nullable: false),
					assurance_level = table.Column<string>(type: "text", nullable: false),
					provider_id = table.Column<Guid>(type: "uuid", nullable: true),
					target_group = table.Column<string>(type: "text", nullable: true),
					target_application = table.Column<string>(type: "text", nullable: true),
					is_active = table.Column<bool>(type: "boolean", nullable: false),
					created_at = table.Column<DateTime>(type: "timestamp", nullable: false),
					updated_at = table.Column<DateTime>(type: "timestamp", nullable: false)
				},
				constraints: table =>
				{
					table.PrimaryKey("pk_authentication_policies", x => x.id);
					table.ForeignKey(
						name: "fk_authentication_policies_identity_providers_provider_id",
						column: x => x.provider_id,
						principalTable: "identity_providers",
						principalColumn: "id",
						onDelete: ReferentialAction.SetNull);
				});

			migrationBuilder.CreateTable(
				name: "ans_delegation_logs",
				columns: table => new
				{
					id = table.Column<Guid>(type: "uuid", nullable: false),
					user_id = table.Column<Guid>(type: "uuid", nullable: false),
					rpps_number = table.Column<string>(type: "text", nullable: false),
					nfc_badge_uid = table.Column<string>(type: "text", nullable: false),
					verification_method = table.Column<string>(type: "text", nullable: false),
					paired_at = table.Column<DateTime>(type: "timestamp", nullable: false),
					admin_user_id = table.Column<Guid>(type: "uuid", nullable: true),
					is_declared_to_government = table.Column<bool>(type: "boolean", nullable: false)
				},
				constraints: table =>
				{
					table.PrimaryKey("pk_ans_delegation_logs", x => x.id);
					table.ForeignKey(
						name: "fk_ans_delegation_logs_users_admin_user_id",
						column: x => x.admin_user_id,
						principalTable: "users",
						principalColumn: "id",
						onDelete: ReferentialAction.SetNull);
					table.ForeignKey(
						name: "fk_ans_delegation_logs_users_user_id",
						column: x => x.user_id,
						principalTable: "users",
						principalColumn: "id",
						onDelete: ReferentialAction.Cascade);
				});

			migrationBuilder.CreateTable(
				name: "audit_logs",
				columns: table => new
				{
					id = table.Column<Guid>(type: "uuid", nullable: false),
					user_id = table.Column<Guid>(type: "uuid", nullable: true),
					action = table.Column<string>(type: "text", nullable: false),
					resource_type = table.Column<string>(type: "text", nullable: true),
					resource_id = table.Column<Guid>(type: "uuid", nullable: true),
					old_values = table.Column<string>(type: "text", nullable: true),
					new_values = table.Column<string>(type: "text", nullable: true),
					ip_address = table.Column<string>(type: "text", nullable: true),
					user_agent = table.Column<string>(type: "text", nullable: true),
					created_at = table.Column<DateTime>(type: "timestamp", nullable: false)
				},
				constraints: table =>
				{
					table.PrimaryKey("pk_audit_logs", x => x.id);
					table.ForeignKey(
						name: "fk_audit_logs_users_user_id",
						column: x => x.user_id,
						principalTable: "users",
						principalColumn: "id");
				});

			migrationBuilder.CreateTable(
				name: "federation_tokens",
				columns: table => new
				{
					id = table.Column<Guid>(type: "uuid", nullable: false),
					user_id = table.Column<Guid>(type: "uuid", nullable: false),
					provider_type = table.Column<string>(type: "text", nullable: false),
					token_reference = table.Column<string>(type: "text", nullable: false),
					external_subject_id = table.Column<string>(type: "text", nullable: true),
					assurance_level = table.Column<string>(type: "text", nullable: true),
					issued_at = table.Column<DateTime>(type: "timestamp", nullable: false),
					expires_at = table.Column<DateTime>(type: "timestamp", nullable: false),
					is_revoked = table.Column<bool>(type: "boolean", nullable: false)
				},
				constraints: table =>
				{
					table.PrimaryKey("pk_federation_tokens", x => x.id);
					table.ForeignKey(
						name: "fk_federation_tokens_users_user_id",
						column: x => x.user_id,
						principalTable: "users",
						principalColumn: "id",
						onDelete: ReferentialAction.Cascade);
				});

			migrationBuilder.CreateTable(
				name: "kiosk_sessions",
				columns: table => new
				{
					id = table.Column<Guid>(type: "uuid", nullable: false),
					user_id = table.Column<Guid>(type: "uuid", nullable: false),
					session_token = table.Column<string>(type: "text", nullable: false),
					badge_uid = table.Column<string>(type: "text", nullable: false),
					nfc_uid = table.Column<string>(type: "text", nullable: false),
					expires_at = table.Column<DateTime>(type: "timestamp", nullable: false),
					created_at = table.Column<DateTime>(type: "timestamp", nullable: false)
				},
				constraints: table =>
				{
					table.PrimaryKey("pk_kiosk_sessions", x => x.id);
					table.ForeignKey(
						name: "fk_kiosk_sessions_users_user_id",
						column: x => x.user_id,
						principalTable: "users",
						principalColumn: "id",
						onDelete: ReferentialAction.Cascade);
				});

			migrationBuilder.CreateTable(
				name: "user_devices",
				columns: table => new
				{
					id = table.Column<Guid>(type: "uuid", nullable: false),
					user_id = table.Column<Guid>(type: "uuid", nullable: false),
					device_type = table.Column<string>(type: "text", nullable: false),
					device_name = table.Column<string>(type: "text", nullable: false),
					device_identifier = table.Column<string>(type: "text", nullable: true),
					device_serial = table.Column<string>(type: "text", nullable: true),
					status = table.Column<string>(type: "text", nullable: false),
					assurance_level = table.Column<string>(type: "text", nullable: false),
					is_primary = table.Column<bool>(type: "boolean", nullable: false),
					is_certified = table.Column<bool>(type: "boolean", nullable: false),
					certification_reference = table.Column<string>(type: "text", nullable: true),
					expires_at = table.Column<DateTime>(type: "timestamp", nullable: true),
					last_used_at = table.Column<DateTime>(type: "timestamp", nullable: true),
					created_at = table.Column<DateTime>(type: "timestamp", nullable: false),
					updated_at = table.Column<DateTime>(type: "timestamp", nullable: false),
					registered_by = table.Column<Guid>(type: "uuid", nullable: true)
				},
				constraints: table =>
				{
					table.PrimaryKey("pk_user_devices", x => x.id);
					table.ForeignKey(
						name: "fk_user_devices_users_registered_by",
						column: x => x.registered_by,
						principalTable: "users",
						principalColumn: "id",
						onDelete: ReferentialAction.SetNull);
					table.ForeignKey(
						name: "fk_user_devices_users_user_id",
						column: x => x.user_id,
						principalTable: "users",
						principalColumn: "id",
						onDelete: ReferentialAction.Cascade);
				});

			migrationBuilder.CreateTable(
				name: "user_permissions",
				columns: table => new
				{
					id = table.Column<Guid>(type: "uuid", nullable: false),
					user_id = table.Column<Guid>(type: "uuid", nullable: false),
					application_id = table.Column<Guid>(type: "uuid", nullable: false),
					permission_level = table.Column<string>(type: "text", nullable: false),
					granted_at = table.Column<DateTime>(type: "timestamp", nullable: false),
					granted_by = table.Column<Guid>(type: "uuid", nullable: true),
					expires_at = table.Column<DateTime>(type: "timestamp", nullable: true)
				},
				constraints: table =>
				{
					table.PrimaryKey("pk_user_permissions", x => x.id);
					table.ForeignKey(
						name: "fk_user_permissions_applications_application_id",
						column: x => x.application_id,
						principalTable: "applications",
						principalColumn: "id",
						onDelete: ReferentialAction.Cascade);
					table.ForeignKey(
						name: "fk_user_permissions_users_granted_by",
						column: x => x.granted_by,
						principalTable: "users",
						principalColumn: "id");
					table.ForeignKey(
						name: "fk_user_permissions_users_user_id",
						column: x => x.user_id,
						principalTable: "users",
						principalColumn: "id",
						onDelete: ReferentialAction.Cascade);
				});

			migrationBuilder.CreateTable(
				name: "user_pins",
				columns: table => new
				{
					id = table.Column<Guid>(type: "uuid", nullable: false),
					user_id = table.Column<Guid>(type: "uuid", nullable: false),
					badge_uid = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
					pin_hash = table.Column<string>(type: "text", nullable: false),
					is_active = table.Column<bool>(type: "boolean", nullable: false),
					created_at = table.Column<DateTime>(type: "timestamp", nullable: false),
					updated_at = table.Column<DateTime>(type: "timestamp", nullable: false),
					created_by = table.Column<Guid>(type: "uuid", nullable: true)
				},
				constraints: table =>
				{
					table.PrimaryKey("pk_user_pins", x => x.id);
					table.ForeignKey(
						name: "fk_user_pins_users_created_by",
						column: x => x.created_by,
						principalTable: "users",
						principalColumn: "id");
					table.ForeignKey(
						name: "fk_user_pins_users_user_id",
						column: x => x.user_id,
						principalTable: "users",
						principalColumn: "id",
						onDelete: ReferentialAction.Cascade);
				});

			migrationBuilder.CreateTable(
				name: "workflows",
				columns: table => new
				{
					id = table.Column<Guid>(type: "uuid", nullable: false),
					type = table.Column<string>(type: "text", nullable: false),
					user_id = table.Column<Guid>(type: "uuid", nullable: false),
					status = table.Column<string>(type: "text", nullable: false),
					assigned_to = table.Column<Guid>(type: "uuid", nullable: true),
					assigned_by = table.Column<Guid>(type: "uuid", nullable: true),
					comments = table.Column<string>(type: "text", nullable: true),
					form_data = table.Column<string>(type: "jsonb", nullable: true),
					created_at = table.Column<DateTime>(type: "timestamp", nullable: false),
					updated_at = table.Column<DateTime>(type: "timestamp", nullable: false)
				},
				constraints: table =>
				{
					table.PrimaryKey("pk_workflows", x => x.id);
					table.ForeignKey(
						name: "fk_workflows_users_assigned_by",
						column: x => x.assigned_by,
						principalTable: "users",
						principalColumn: "id");
					table.ForeignKey(
						name: "fk_workflows_users_assigned_to",
						column: x => x.assigned_to,
						principalTable: "users",
						principalColumn: "id");
					table.ForeignKey(
						name: "fk_workflows_users_user_id",
						column: x => x.user_id,
						principalTable: "users",
						principalColumn: "id",
						onDelete: ReferentialAction.Cascade);
				});

			migrationBuilder.CreateIndex(
				name: "ix_ans_delegation_logs_admin_user_id",
				table: "ans_delegation_logs",
				column: "admin_user_id");

			migrationBuilder.CreateIndex(
				name: "ix_ans_delegation_logs_user_id",
				table: "ans_delegation_logs",
				column: "user_id");

			migrationBuilder.CreateIndex(
				name: "ix_applications_client_id",
				table: "applications",
				column: "client_id",
				unique: true);

			migrationBuilder.CreateIndex(
				name: "ix_audit_logs_user_id",
				table: "audit_logs",
				column: "user_id");

			migrationBuilder.CreateIndex(
				name: "ix_authentication_policies_provider_id",
				table: "authentication_policies",
				column: "provider_id");

			migrationBuilder.CreateIndex(
				name: "ix_federation_tokens_user_id_provider_type",
				table: "federation_tokens",
				columns: new[] { "user_id", "provider_type" });

			migrationBuilder.CreateIndex(
				name: "ix_identity_providers_provider_type",
				table: "identity_providers",
				column: "provider_type",
				unique: true);

			migrationBuilder.CreateIndex(
				name: "ix_kiosk_sessions_user_id",
				table: "kiosk_sessions",
				column: "user_id");

			migrationBuilder.CreateIndex(
				name: "ix_migration_phases_is_current_phase",
				table: "migration_phases",
				column: "is_current_phase");

			migrationBuilder.CreateIndex(
				name: "ix_user_devices_device_identifier",
				table: "user_devices",
				column: "device_identifier");

			migrationBuilder.CreateIndex(
				name: "ix_user_devices_registered_by",
				table: "user_devices",
				column: "registered_by");

			migrationBuilder.CreateIndex(
				name: "ix_user_devices_user_id_device_type",
				table: "user_devices",
				columns: new[] { "user_id", "device_type" });

			migrationBuilder.CreateIndex(
				name: "ix_user_permissions_application_id",
				table: "user_permissions",
				column: "application_id");

			migrationBuilder.CreateIndex(
				name: "ix_user_permissions_granted_by",
				table: "user_permissions",
				column: "granted_by");

			migrationBuilder.CreateIndex(
				name: "ix_user_permissions_user_id",
				table: "user_permissions",
				column: "user_id");

			migrationBuilder.CreateIndex(
				name: "ix_user_pins_badge_uid",
				table: "user_pins",
				column: "badge_uid",
				unique: true);

			migrationBuilder.CreateIndex(
				name: "ix_user_pins_created_by",
				table: "user_pins",
				column: "created_by");

			migrationBuilder.CreateIndex(
				name: "ix_user_pins_user_id",
				table: "user_pins",
				column: "user_id");

			migrationBuilder.CreateIndex(
				name: "ix_users_ad_guid",
				table: "users",
				column: "ad_guid",
				unique: true);

			migrationBuilder.CreateIndex(
				name: "ix_users_badge_uid",
				table: "users",
				column: "badge_uid");

			migrationBuilder.CreateIndex(
				name: "ix_users_email",
				table: "users",
				column: "email");

			migrationBuilder.CreateIndex(
				name: "ix_workflows_assigned_by",
				table: "workflows",
				column: "assigned_by");

			migrationBuilder.CreateIndex(
				name: "ix_workflows_assigned_to",
				table: "workflows",
				column: "assigned_to");

			migrationBuilder.CreateIndex(
				name: "ix_workflows_user_id",
				table: "workflows",
				column: "user_id");
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropTable(
				name: "ans_delegation_logs");

			migrationBuilder.DropTable(
				name: "audit_logs");

			migrationBuilder.DropTable(
				name: "authentication_policies");

			migrationBuilder.DropTable(
				name: "federation_tokens");

			migrationBuilder.DropTable(
				name: "kiosk_sessions");

			migrationBuilder.DropTable(
				name: "migration_phases");

			migrationBuilder.DropTable(
				name: "user_devices");

			migrationBuilder.DropTable(
				name: "user_permissions");

			migrationBuilder.DropTable(
				name: "user_pins");

			migrationBuilder.DropTable(
				name: "workflows");

			migrationBuilder.DropTable(
				name: "identity_providers");

			migrationBuilder.DropTable(
				name: "applications");

			migrationBuilder.DropTable(
				name: "users");
		}
	}
}
