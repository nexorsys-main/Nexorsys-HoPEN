using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nexorsys.Identity.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CredentialAuditRedaction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "credential_digest_sha256",
                table: "ans_delegation_logs",
                type: "text",
                nullable: true);

            // Historical rows stored the raw NFC UID as audit data. Remove it;
            // prior digest recovery is intentionally not attempted by this migration.
            migrationBuilder.Sql("UPDATE ans_delegation_logs SET nfc_badge_uid = '' WHERE nfc_badge_uid <> '';");
            migrationBuilder.Sql(@"UPDATE audit_logs
SET old_values = CASE WHEN old_values ~* '(password|passwd|pin|badge(uid)?|nfcbadgeuid|cpsid|fidoid|(access|refresh|session)?token|secret|credential|api[_-]?key|private[_ -]?key)\s*[:=]'
                       THEN '[REDACTED HISTORICAL SENSITIVE VALUE]' ELSE old_values END,
    new_values = CASE WHEN new_values ~* '(password|passwd|pin|badge(uid)?|nfcbadgeuid|cpsid|fidoid|(access|refresh|session)?token|secret|credential|api[_-]?key|private[_ -]?key)\s*[:=]'
                       THEN '[REDACTED HISTORICAL SENSITIVE VALUE]' ELSE new_values END
WHERE old_values ~* '(password|passwd|pin|badge(uid)?|nfcbadgeuid|cpsid|fidoid|(access|refresh|session)?token|secret|credential|api[_-]?key|private[_ -]?key)\s*[:=]'
   OR new_values ~* '(password|passwd|pin|badge(uid)?|nfcbadgeuid|cpsid|fidoid|(access|refresh|session)?token|secret|credential|api[_-]?key|private[_ -]?key)\s*[:=]';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "credential_digest_sha256",
                table: "ans_delegation_logs");
        }
    }
}
