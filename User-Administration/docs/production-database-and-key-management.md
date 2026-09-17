# Production database, cryptographic-key operations, and Vault encryption

## Database schema deployment

The API does not apply schema changes at startup. Build and review an idempotent EF migration script for the release, take and verify a database backup, apply the script using a deployment identity with DDL rights, then start the API using the least-privilege runtime identity. The runtime identity must not have schema-alteration privileges. Startup and `/api/health/status` fail readiness when PostgreSQL is unavailable or unapplied EF migrations remain.

For upgrades, stop or drain API instances, capture a restorable backup, apply the reviewed forward migration, verify migration history and critical row counts, then start the new application version. Prefer forward repair migrations. Rollback is a coordinated restore to a new database from the verified pre-upgrade backup; the destructive legacy `init_db.sql` rollback script is disabled. A database restore must be rehearsed before customer rollout. Fresh-schema migration was exercised against a disposable PostgreSQL 16.9 container; customer upgrade/restore remains external validation.

## Data Protection key ring

Production requires `DataProtection:KeysDirectory` to name an existing customer-controlled directory and `DataProtection:EncryptionCertificateThumbprint` to identify an unexpired certificate with an accessible private key in the machine certificate store (Windows: `LocalMachine\My`; other supported hosts: the service identity's `CurrentUser\My`). API startup fails if either is absent or unusable. Startup rejects a directory reparse point and performs a write/read/delete probe as the running service identity. On Windows, the directory DACL must grant Modify directly to that service SID; existing `*.xml` key files must grant Read to it, and every Allow ACE on the directory or key files must be for that SID, SYSTEM, BUILTIN Administrators, or an explicitly approved SID in `DataProtection:AllowedAccessSids`. Broad or unknown principals and key-file reparse points fail startup. On Unix, the directory must have mode `0700` (owner read/write/execute only). Provision the directory and ACL before startup; do not place it in the application publish directory, source tree, shared web root, or a user-writable location.

For certificate rollover, set `DataProtection:EncryptionCertificateThumbprint` to the new active certificate and configure `DataProtection:DecryptionCertificateThumbprints` as the list of prior certificate thumbprints whose private keys must remain available to decrypt existing key-ring entries. All API nodes must have the new certificate and every retained decryption private key before switching the active thumbprint. The active certificate must be currently valid; retained decryption certificates must have accessible private keys but may be expired. Keep each old private key for as long as any retained Data Protection key may need it for restart, cookie/session recovery, backup restore, or a rolling deployment. Remove an old thumbprint/private key only after its encrypted keys and protected payloads are outside the supported retention/restore window. This process is covered by a local two-certificate restart regression, but customer certificate-store ACLs and production rollover remain host validation.

Back up the key ring and the matching encryption private key together into the customer's protected backup system. For certificate rotation, install the new certificate while retaining the old decryption certificate, configure new key protection only after all instances can decrypt prior keys, verify restart/rolling restart, then retire the old private key only after the retention and restore window has elapsed. Never copy private keys into configuration or source. Validate ACL denial under an unprivileged identity and document recovery ownership before production use.

Development uses a distinct Data Protection application name and is not evidence of production persistence or access control.

## Vault Encryption (1.5.2)

NexorSys Identity uses ASP.NET Core Data Protection (`IDataProtector`) for AES-256-GCM authenticated encryption of Vault credentials.
All stored secrets (`VaultEntry.EncryptedSecret`) contain the full authenticated payload: ciphertext, nonce/IV, authentication tag, key version, and algorithm metadata.
The production environment **must** provision durable data protection keys using the encryption certificates outlined above. The system enforces tenant isolation and optimistic concurrency (PostgreSQL `xmin`) during key rotation.

Plaintext credentials are never exposed via REST API. The payload is decrypted strictly during the authenticated Windows Agent release workflow using authorized session identifiers and access grants.

## Database credentials and legacy secrets

Database and service credentials must be injected through the customer-approved secret store or deployment environment. Legacy PowerShell scripts now require `NEXORSYS_DB_PASSWORD` rather than an embedded password. The simulation/seed scripts are not production provisioning. Secrets detected in repository history require rotation even when the working-tree value is removed. Rotate any potentially live PostgreSQL, SQL Server, SMTP, JWT-signing, application-client, backup-encryption or certificate credential before deployment.

## License signing

Identity/Kiosk verifies ECDSA P-256/SHA-256 signed license tokens using `Licensing:PublicKeyPem`. The vendor signing private key must remain outside this repository and outside the deployed Identity/Kiosk process. Production provisioning must configure the public verification key and the exact product/version policy. No universal activation key or example production signature is shipped.
