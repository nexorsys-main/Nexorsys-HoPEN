using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nexorsys.Identity.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class WorkstationLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "approved_at",
                table: "workstations",
                type: "timestamp",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "decommissioned_at",
                table: "workstations",
                type: "timestamp",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "enrolled_at",
                table: "workstations",
                type: "timestamp",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "enrollment_state",
                table: "workstations",
                type: "text",
                nullable: false,
                defaultValue: "PENDING_APPROVAL");

            migrationBuilder.AddColumn<DateTime>(
                name: "last_validated_at",
                table: "workstations",
                type: "timestamp",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "replaced_by_workstation_id",
                table: "workstations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "revoked_at",
                table: "workstations",
                type: "timestamp",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "workstation_id",
                table: "kiosk_sessions",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "approved_at",
                table: "workstations");

            migrationBuilder.DropColumn(
                name: "decommissioned_at",
                table: "workstations");

            migrationBuilder.DropColumn(
                name: "enrolled_at",
                table: "workstations");

            migrationBuilder.DropColumn(
                name: "enrollment_state",
                table: "workstations");

            migrationBuilder.DropColumn(
                name: "last_validated_at",
                table: "workstations");

            migrationBuilder.DropColumn(
                name: "replaced_by_workstation_id",
                table: "workstations");

            migrationBuilder.DropColumn(
                name: "revoked_at",
                table: "workstations");

            migrationBuilder.DropColumn(
                name: "workstation_id",
                table: "kiosk_sessions");
        }
    }
}
