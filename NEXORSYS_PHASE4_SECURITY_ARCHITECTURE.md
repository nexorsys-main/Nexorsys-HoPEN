# NexorSys Phase 4 Security Architecture and Operational Contracts

This document describes implemented contracts and explicitly separates them from unfinished components. It is not evidence of external validation.

## Device enrollment — implemented server workflow

1. An authenticated organization administrator creates a workstation record. The server assigns its tenant and starts it in `PENDING_APPROVAL`; client input cannot choose tenant, active status, or certificate thumbprint.
2. An administrator associates the thumbprint of a customer-issued Client Authentication certificate. A unique filtered database index prevents one thumbprint from binding to two workstations. This moves the record to `ENROLLED`, not `ACTIVE`.
3. The target workstation presents the certificate over TLS to `POST /api/workstations/enrollment/{id}/prove-possession`. The server validates validity dates, system trust chain, revocation online, Client Authentication EKU, and exact thumbprint. A successful proof atomically activates the device and records the audit event.
4. Production Kiosk/Fleet authentication checks certificate chain/usage, organization, workstation ID, lifecycle state `ACTIVE`, legacy `IsActive`, and exact certificate binding. Revocation, replacement, and decommission set `IsActive=false` and end active user and application sessions.

The organization supplies the CA and issuance policy. This application does not issue certificates, create a CA, provision private keys, validate customer PKI operations, or implement automated renewal. Existing workstation rows are assigned `PENDING_APPROVAL` by migration and must be reviewed and re-enrolled. The PostgreSQL migration and customer certificate ceremony remain untested.

## Agent identity and release API — partial; Windows Agent and IPC not implemented

The backend now has a separate Agent certificate thumbprint per active workstation. An administrator associates the customer-issued Agent certificate; the Agent proves private-key possession over TLS before the server recognizes it. The mTLS-only `/api/agent/v1/vault-release` endpoints derive organization/workstation from that certificate and issue/redeem short-lived grants. This is a server-side contract only: there is still no Agent service executable or active IPC listener. The Credential Provider intentionally fails closed; it does not authenticate over localhost HTTP. The following remains required, not shipped:

- A Windows service runs under a dedicated, non-interactive virtual service identity with only required private-key and application-launch rights.
- Local IPC must use a versioned named-pipe protocol with a restrictive DACL, server identity validation, client impersonation/token inspection, bounded message size, operation allowlist, and per-operation authorization. There must be no loopback HTTP fallback.
- Requests bind organization, workstation, Windows logon SID/session ID, user-session ID, application-session ID, application identity, request ID, issue time, and expiry. Agent derives machine/user context from the OS; callers cannot assert it freely.
- The service keeps a bounded replay cache keyed by authenticated principal + request ID; duplicate IDs, expired requests, malformed messages, wrong session/application context, and unsupported protocol versions are rejected. Responses are correlated and time-bounded.
- API authentication uses a machine-protected private key/certificate associated with the enrolled workstation and an Agent-specific identity/role. No shared secret is embedded in the service, Credential Provider, or Kiosk binary.
- Every denial and security-relevant operation is audited with correlation ID and non-sensitive identifiers only. Secret values, PINs, badge UIDs, tokens, and private key material never enter logs.

Until the service, installer ACLs, exclusive key access, Windows caller/process/user/session binding, and adversarial IPC tests exist, the mTLS release API must not be deployed with its private key accessible to ordinary processes. Credential Provider login and end-to-end application credential delivery remain unavailable.

## Vault credential release — not implemented

`VaultService.CanUseAsync` remains an authorization decision only. The dedicated Agent release service now issues 20-second one-use grants containing no plaintext; only a hash of the random redemption token is stored. Redemption rechecks organization, active/unlocked user, recent session, active/proven Agent workstation, application and non-read permission, matching application session, and active/unexpired Vault entry inside a serializable transaction. A conditional update consumes the grant once. The credential is decrypted only after successful consumption and is returned only through the distinct mTLS Agent endpoint with `Cache-Control: no-store`; ordinary browser REST still never returns it.

The mTLS grant/redeem server path is implemented, but cannot be treated as end-to-end credential release until a real Agent exclusively owns the Agent private key, validates Windows caller/process/session identity, and calls the API only for an approved application-specific integration. Delivery via arbitrary process launch or clipboard is not accepted. No app delivery integration is implemented.

## Authorization model — partial, not endpoint-complete

Tenant query filters and selected explicit organization checks are defense in depth, not a complete authorization solution. Existing role policies remain in use. The canonical intended calculation is:

`authenticated active user → organization membership → user/group roles → role permissions → resource/action + workstation/session constraints`

Missing or ambiguous policy must deny. Resource ownership and tenant checks belong server-side and cannot be inferred from UI state or route names. A full endpoint inventory, policy conversion, and adversarial role/permission-removal tests have not been completed; do not treat the current policy set as complete.

## Database, secrets, and deployment assumptions

- Apply EF migrations as an explicit reviewed deployment step; this repository does not run automatic production migrations at startup. Back up before upgrade and test restore/rollback in a disposable PostgreSQL environment first.
- The workstation migration sets legacy rows to `PENDING_APPROVAL`. Before applying it, inspect the target database for duplicate normalized certificate thumbprints; the unique index will intentionally reject duplicates. Resolve ownership with administrators before the migration.
- Production must externally supply PostgreSQL TLS credentials, a strong JWT signing key, issuer/audience, exact HTTPS origins, TLS/mTLS termination, rate-limit policy, and persistent protected ASP.NET Data Protection keys. No production secret belongs in source, frontend configuration, or installer.
- Health/readiness must distinguish process liveness from database/CA/Agent readiness. The current health routes do not prove production dependency readiness.
- Backups contain customer data and are excluded from any package. They must be encrypted, access-controlled, retention-limited, and restore-tested by the deployment owner.

## Directory and external authentication providers

Production LDAP must use LDAPS with ordinary platform certificate-chain and hostname validation; invalid certificates must cause a hard failure. Stable object identifiers, tenant-specific bases/credentials, group-to-role mapping, disable propagation, bounded timeouts, retries with backoff, and audit of synchronization outcomes are required. The current directory integration is not validated against an enterprise directory and does not meet this full contract.

NFC UID is an identifier, not proof of possession. The current PIN verifier checks BCrypt hashes; NFC hardware ceremony remains externally unvalidated. CPS/e-CPS/PSC placeholder providers must reject unvalidated assertions; provider success requires issuer-backed verification. FIDO2 and hardware-backed key ceremonies are not implemented in this slice.

## Signing and Windows lifecycle assumptions

The Credential Provider DLL/MSI currently available is unsigned engineering output. Commercial distribution requires an organization-controlled Authenticode signing certificate, reproducible signing pipeline, architecture/OS checks, secure service/pipe ACL setup, rollback, registry cleanup, and Winlogon/shared-user tests. Those are not yet an installer implementation. Never install the current engineering MSI on a production workstation.

## Validation boundary

Internally executed evidence is limited to the security test project and compilation recorded in `NEXORSYS_PHASE4_IMPLEMENTATION_REPORT.md`. No production PostgreSQL, customer CA, LDAPS directory, supported badge reader, Windows logon integration, signed package, penetration test, or customer deployment has been externally validated.
