using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nexorsys.Identity.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class VaultCredentialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "credential_type",
                table: "vault_entries",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "last_rotated_at",
                table: "vault_entries",
                type: "timestamp",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "name",
                table: "vault_entries",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "next_rotation_at",
                table: "vault_entries",
                type: "timestamp",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "nonce",
                table: "vault_entries",
                type: "bytea",
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<DateTime>(
                name: "revoked_at",
                table: "vault_entries",
                type: "timestamp",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "site_id",
                table: "vault_entries",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "version",
                table: "vault_entries",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "vault_entries",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "credential_type",
                table: "vault_entries");

            migrationBuilder.DropColumn(
                name: "last_rotated_at",
                table: "vault_entries");

            migrationBuilder.DropColumn(
                name: "name",
                table: "vault_entries");

            migrationBuilder.DropColumn(
                name: "next_rotation_at",
                table: "vault_entries");

            migrationBuilder.DropColumn(
                name: "nonce",
                table: "vault_entries");

            migrationBuilder.DropColumn(
                name: "revoked_at",
                table: "vault_entries");

            migrationBuilder.DropColumn(
                name: "site_id",
                table: "vault_entries");

            migrationBuilder.DropColumn(
                name: "version",
                table: "vault_entries");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "vault_entries");
        }
    }
}
