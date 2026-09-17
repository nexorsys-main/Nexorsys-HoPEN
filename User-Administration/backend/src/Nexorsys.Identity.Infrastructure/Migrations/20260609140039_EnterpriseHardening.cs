using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nexorsys.Identity.Infrastructure.Migrations
{
	/// <inheritdoc />
	public partial class EnterpriseHardening : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.AddColumn<Guid>(
				name: "enabled_by_user_id",
				table: "feature_flags",
				type: "uuid",
				nullable: true);

			migrationBuilder.AddColumn<DateTime>(
				name: "enabled_from",
				table: "feature_flags",
				type: "timestamp",
				nullable: true);

			migrationBuilder.AddColumn<string>(
				name: "reason",
				table: "feature_flags",
				type: "text",
				nullable: true);

			migrationBuilder.Sql("""
				DO $$
				BEGIN
					IF EXISTS (
						SELECT 1 FROM department_policies
						WHERE id !~* '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'
					) THEN
						RAISE EXCEPTION 'department_policies contains non-UUID identifiers; resolve them before EnterpriseHardening migration';
					END IF;
				END $$;
				ALTER TABLE department_policies ALTER COLUMN id TYPE uuid USING id::uuid;
				""");

			migrationBuilder.AddColumn<TimeSpan>(
				name: "allowed_access_end",
				table: "department_policies",
				type: "interval",
				nullable: false,
				defaultValue: new TimeSpan(0, 0, 0, 0, 0));

			migrationBuilder.AddColumn<TimeSpan>(
				name: "allowed_access_start",
				table: "department_policies",
				type: "interval",
				nullable: false,
				defaultValue: new TimeSpan(0, 0, 0, 0, 0));

			migrationBuilder.AddColumn<string>(
				name: "department",
				table: "department_policies",
				type: "text",
				nullable: false,
				defaultValue: "");

			migrationBuilder.AddColumn<string>(
				name: "required_risk_level",
				table: "department_policies",
				type: "text",
				nullable: false,
				defaultValue: "");

			migrationBuilder.AddColumn<string>(
				name: "reason",
				table: "authentication_events",
				type: "text",
				nullable: true);

			migrationBuilder.CreateIndex(
				name: "ix_authentication_events_timestamp_user_id_workstation_id",
				table: "authentication_events",
				columns: new[] { "timestamp", "user_id", "workstation_id" });
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropIndex(
				name: "ix_authentication_events_timestamp_user_id_workstation_id",
				table: "authentication_events");

			migrationBuilder.DropColumn(
				name: "enabled_by_user_id",
				table: "feature_flags");

			migrationBuilder.DropColumn(
				name: "enabled_from",
				table: "feature_flags");

			migrationBuilder.DropColumn(
				name: "reason",
				table: "feature_flags");

			migrationBuilder.DropColumn(
				name: "allowed_access_end",
				table: "department_policies");

			migrationBuilder.DropColumn(
				name: "allowed_access_start",
				table: "department_policies");

			migrationBuilder.DropColumn(
				name: "department",
				table: "department_policies");

			migrationBuilder.DropColumn(
				name: "required_risk_level",
				table: "department_policies");

			migrationBuilder.DropColumn(
				name: "reason",
				table: "authentication_events");

			migrationBuilder.Sql("ALTER TABLE department_policies ALTER COLUMN id TYPE text USING id::text;");
		}
	}
}
