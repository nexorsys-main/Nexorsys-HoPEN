using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nexorsys.Identity.Infrastructure.Migrations
{
	/// <inheritdoc />
	public partial class UnifiedAuthSessions : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.AddColumn<string>(
				name: "authentication_method",
				table: "user_sessions",
				type: "text",
				nullable: true);

			migrationBuilder.AddColumn<DateTime>(
				name: "last_activity_at",
				table: "user_sessions",
				type: "timestamp",
				nullable: true);

			migrationBuilder.AddColumn<string>(
				name: "session_token",
				table: "user_sessions",
				type: "text",
				nullable: true);

			migrationBuilder.AddColumn<string>(
				name: "status",
				table: "user_sessions",
				type: "text",
				nullable: true);

			migrationBuilder.CreateTable(
				name: "pin_reset_requests",
				columns: table => new
				{
					id = table.Column<Guid>(type: "uuid", nullable: false),
					user_id = table.Column<Guid>(type: "uuid", nullable: false),
					badge_uid = table.Column<string>(type: "text", nullable: false),
					workstation_id = table.Column<Guid>(type: "uuid", nullable: true),
					requested_at = table.Column<DateTime>(type: "timestamp", nullable: false),
					approved_at = table.Column<DateTime>(type: "timestamp", nullable: true),
					approved_by = table.Column<Guid>(type: "uuid", nullable: true),
					status = table.Column<string>(type: "text", nullable: false),
					reason = table.Column<string>(type: "text", nullable: true)
				},
				constraints: table =>
				{
					table.PrimaryKey("pk_pin_reset_requests", x => x.id);
					table.ForeignKey(
						name: "fk_pin_reset_requests_users_approved_by",
						column: x => x.approved_by,
						principalTable: "users",
						principalColumn: "id",
						onDelete: ReferentialAction.SetNull);
					table.ForeignKey(
						name: "fk_pin_reset_requests_users_user_id",
						column: x => x.user_id,
						principalTable: "users",
						principalColumn: "id",
						onDelete: ReferentialAction.Cascade);
					table.ForeignKey(
						name: "fk_pin_reset_requests_workstations_workstation_id",
						column: x => x.workstation_id,
						principalTable: "workstations",
						principalColumn: "id",
						onDelete: ReferentialAction.SetNull);
				});

			migrationBuilder.CreateTable(
				name: "session_events",
				columns: table => new
				{
					id = table.Column<Guid>(type: "uuid", nullable: false),
					session_id = table.Column<Guid>(type: "uuid", nullable: false),
					event_type = table.Column<string>(type: "text", nullable: false),
					event_description = table.Column<string>(type: "text", nullable: true),
					created_at = table.Column<DateTime>(type: "timestamp", nullable: false)
				},
				constraints: table =>
				{
					table.PrimaryKey("pk_session_events", x => x.id);
					table.ForeignKey(
						name: "fk_session_events_user_sessions_session_id",
						column: x => x.session_id,
						principalTable: "user_sessions",
						principalColumn: "id",
						onDelete: ReferentialAction.Cascade);
				});

			migrationBuilder.CreateTable(
				name: "workstation_sessions",
				columns: table => new
				{
					id = table.Column<Guid>(type: "uuid", nullable: false),
					workstation_id = table.Column<Guid>(type: "uuid", nullable: false),
					current_user_id = table.Column<Guid>(type: "uuid", nullable: true),
					session_id = table.Column<Guid>(type: "uuid", nullable: true),
					session_started_at = table.Column<DateTime>(type: "timestamp", nullable: false),
					session_ended_at = table.Column<DateTime>(type: "timestamp", nullable: true),
					last_badge_seen = table.Column<string>(type: "text", nullable: true)
				},
				constraints: table =>
				{
					table.PrimaryKey("pk_workstation_sessions", x => x.id);
					table.ForeignKey(
						name: "fk_workstation_sessions_user_sessions_session_id",
						column: x => x.session_id,
						principalTable: "user_sessions",
						principalColumn: "id",
						onDelete: ReferentialAction.SetNull);
					table.ForeignKey(
						name: "fk_workstation_sessions_users_current_user_id",
						column: x => x.current_user_id,
						principalTable: "users",
						principalColumn: "id",
						onDelete: ReferentialAction.SetNull);
					table.ForeignKey(
						name: "fk_workstation_sessions_workstations_workstation_id",
						column: x => x.workstation_id,
						principalTable: "workstations",
						principalColumn: "id",
						onDelete: ReferentialAction.Cascade);
				});

			migrationBuilder.CreateIndex(
				name: "ix_pin_reset_requests_approved_by",
				table: "pin_reset_requests",
				column: "approved_by");

			migrationBuilder.CreateIndex(
				name: "ix_pin_reset_requests_user_id",
				table: "pin_reset_requests",
				column: "user_id");

			migrationBuilder.CreateIndex(
				name: "ix_pin_reset_requests_workstation_id",
				table: "pin_reset_requests",
				column: "workstation_id");

			migrationBuilder.CreateIndex(
				name: "ix_session_events_session_id",
				table: "session_events",
				column: "session_id");

			migrationBuilder.CreateIndex(
				name: "ix_workstation_sessions_current_user_id",
				table: "workstation_sessions",
				column: "current_user_id");

			migrationBuilder.CreateIndex(
				name: "ix_workstation_sessions_session_id",
				table: "workstation_sessions",
				column: "session_id");

			migrationBuilder.CreateIndex(
				name: "ix_workstation_sessions_workstation_id",
				table: "workstation_sessions",
				column: "workstation_id");
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropTable(
				name: "pin_reset_requests");

			migrationBuilder.DropTable(
				name: "session_events");

			migrationBuilder.DropTable(
				name: "workstation_sessions");

			migrationBuilder.DropColumn(
				name: "authentication_method",
				table: "user_sessions");

			migrationBuilder.DropColumn(
				name: "last_activity_at",
				table: "user_sessions");

			migrationBuilder.DropColumn(
				name: "session_token",
				table: "user_sessions");

			migrationBuilder.DropColumn(
				name: "status",
				table: "user_sessions");
		}
	}
}
