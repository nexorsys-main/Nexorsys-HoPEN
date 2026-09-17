# Phase 5 Security Operations and Release Preconditions

## Historical secret exposure and rotation

Repository files and Git history previously contained JWT-shaped test strings in test scripts. The working copies were scrubbed, but ordinary edits do not remove historical Git objects, clones, forks, CI logs, or caches. Treat any corresponding signing material or credential as potentially compromised; do not copy the old values into tickets or logs.

Before customer deployment, the security owner must:

1. Issue a new JWT signing key through the approved secret-management system; update all API instances atomically and invalidate existing refresh/session tokens.
2. Rotate database, SMTP, API-client, backup, directory-bind, and deployment credentials that were ever live or could have been copied into repository history. Rotate only credentials confirmed in the owning environment; do not invent or print values.
3. Audit Git hosting access, CI artifacts/logs, forks, developer clones, and backups. If history rewriting is approved, coordinate a repository-wide rewrite and require fresh clones; rewriting alone does not revoke credentials.
4. Record owner, rotation timestamp, affected systems, session invalidation, and verification evidence in the organization's controlled incident/change record.

No credential rotation has been performed by this repository change.

The `Secret scan` GitHub Actions workflow now runs pinned Gitleaks on each incoming pull-request or push commit range, with secret values redacted from scanner output. This prevents newly introduced findings from silently entering future history; it does not rescan/clear earlier commits or certify that historical values were never live. For an owner-approved local or controlled CI history audit, run `pwsh ./scripts/scan-secrets-git.ps1 -AllHistory`; the script scans all reachable refs and fails on any finding. The current history audit still contains unresolved potential findings. Do not add them to an allowlist or treat the incremental CI check as historical remediation.

## Reverse-proxy client IP trust and rate limits

Login and Kiosk limits are partitioned by the ASP.NET Core connection client IP. If the API is directly exposed, no forwarded-header configuration is needed. If it is behind a reverse proxy, provision `ReverseProxy:KnownProxies` as an array of exact proxy IP literals and optionally `ReverseProxy:ForwardLimit` (default `1`, allowed `1`–`5`) in protected deployment configuration. Forwarded headers are ignored unless at least one proxy IP is explicitly configured; when enabled, only `X-Forwarded-For` and `X-Forwarded-Proto` are processed, and default trusted networks are cleared. Every proxy hop within the configured limit must be enumerated as trusted. Do not use `KnownNetworks = 0.0.0.0/0`, accept forwarded headers from arbitrary clients, or configure a hop count larger than the trusted chain. Network ACLs must also prevent direct untrusted access to the API listener. Validate the actual proxy chain and spoof-resistance during deployment; repository tests only verify the configuration boundary, not customer network topology.

## Data Protection key ring

Production configuration must provide a durable, shared key-ring directory and an encryption certificate with accessible private key through deployment secret management. Startup checks that the directory and existing XML keys are not reparse points, performs a service-identity read/write probe, and enforces a Windows DACL allowlist (service SID, SYSTEM, Administrators, and explicit `DataProtection:AllowedAccessSids`) or Unix directory mode `0700`. Remove inherited broad user access before startup. Confirm certificate expiry/renewal monitoring and backup/restore of encrypted key-ring contents. Validate persistence by restarting every API node and decrypting a value created before restart, then repeat during a rolling deployment. Actual production service identity/certificate-store access, recovery-administrator policy, cluster persistence and restart behavior still require customer-host validation.

Never commit private keys, certificate PFX files, passwords, or production connection strings. Supply thumbprints and secret references only through protected deployment configuration, not frontend settings.

## Signed license revocation feed

Identity validates a signed ECDSA P-256 revocation snapshot locally, rejects stale/invalid signatures, enforces monotonically increasing sequence state, and requires the configured snapshot at production startup. Phase 6 adds an optional mTLS GET client (configured URL + LocalMachine client certificate), a size-bounded JSON contract `{"signedSnapshot":"<payload>.<signature>"}`, verification, monotonic sequence checks, and same-directory atomic cache replacement. Configure `Licensing:RevocationSnapshotFile`, `Licensing:RevocationFeed:Url`, `Licensing:RevocationFeed:ClientCertificateThumbprint`, and `Licensing:RevocationFeed:RefreshMinutes` through protected deployment settings. The cache directory ACL must be restricted to the API service identity and administrators. No NexorSys Admin distribution service or customer feed endpoint is included; feed endpoint deployment, cert provisioning, ACLs, snapshot expiry monitoring and end-to-end revocation delivery remain integration/deployment validation requirements. A local client implementation is not proof of a deployed distribution service.

## Windows Agent ↔ Kiosk executable trust

The named-pipe boundary now requires an exact allowlist on both ends: Agent validates the Kiosk's OS-observed executable path, Authenticode publisher, and SHA-256; Kiosk validates the Agent server process path, publisher, and SHA-256. Provision these protected 64-bit `HKLM\SOFTWARE\NexorSys\Identity\Agent` values from the approved signed release: `KioskExecutablePath`, `KioskPublisherThumbprint`, `KioskExecutableSha256`, `AgentExecutablePath`, `AgentPublisherThumbprint`, and `AgentExecutableSha256`. The Agent reads the first three; the Kiosk reads the latter three. Missing, malformed, or mismatching values deny IPC. Keep the Kiosk path under the managed Program Files installation and restrict the registry key and executable directory to SYSTEM/Administrators plus the intended installer/service identities. On every signed binary upgrade, update its SHA-256 and publisher/path pin through the controlled installer/change process before enabling the new binary; do not use a publisher-only fallback. The actual commercial publisher certificate, signed deployment, registry ACL provisioning and installed-service rehearsal remain external release/deployment validation.

## Release signing

`scripts/Sign-NexorSysArtifact.ps1` requires a current Code Signing EKU certificate with accessible private key, `signtool.exe`, and an HTTPS RFC3161 timestamp service; it signs and verifies EXE/DLL/MSI artifacts. This only documents and implements the local signing path. No commercial certificate, signing key, timestamp, signed MSI, or Winlogon validation is included in the engineering artifacts.

## Vault Administration (1.5.2 Commercial Release)

The Vault administration subsystem securely manages tenant-isolated enterprise application credentials.
- **Data Protection Dependency:** AES-256-GCM Vault encryption strictly relies on the ASP.NET Core Data Protection Key Ring configuration described above.
- **Authorization:** Only authenticated identities holding `Administrator` or `VaultAdministrator` roles within the matching tenant, operating under a valid `identity` license entitlement, can invoke Vault administration endpoints.
- **Agent Enforcement:** Plaintext credential release is structurally excluded from the REST endpoints. The payload is unsealed exclusively during the agent-mediated Winlogon / Credential Provider release process.
