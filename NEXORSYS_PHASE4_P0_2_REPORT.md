# NexorSys Identity + Kiosk — Phase 4 P0.2 report

## Release status

**NOT READY FOR EXTERNAL VALIDATION.** P0.2 adds authoritative server records and authorization checks, but the Windows Agent does not yet call the binding endpoint, its Agent certificate/private-key ACL is not provisioned, and no Windows-host security test has been run. Vault credential release remains hard-disabled: both Agent Vault routes return `503 RELEASE_NOT_CONFIGURED` after authenticating the Agent and recording a denial event. The Credential Provider remains fail-closed.

## Implemented

- Extended the canonical `ApplicationSession` with optional `UserSessionId` and `ApplicationId` foreign keys. Kiosk app-session start records both IDs; Vault authorization requires an exact match to the active parent Identity session and registered `Application`, rather than trusting an application name as identity.
- Added `WindowsUserIdentityBinding`, an administrator-only, organization-scoped mapping of `(WorkstationId, Windows SID)` to an existing NexorSys `User`. Create requires the same-tenant active user and active workstation. Duplicate active mappings conflict. Revocation is audited and revokes related live observations.
- Added `AgentWindowsSessionBinding`, created only on the Agent mTLS route after the server derives organization/workstation from the Agent certificate, validates the registered SID mapping, and finds exactly one active/unlocked/recent `UserSession` for that user and workstation. Ambiguous sessions deny. Bindings expire after 45 seconds; the Agent must refresh. An explicit mTLS end-session route revokes the exact SID/session observation.
- Added organization-scoped application registration fields to the existing `Application` model and an `AdminOnly` registration endpoint. It requires an absolute Windows install root and an X.509 publisher thumbprint. Only delivery mechanism `none` is accepted because no credential adapter is approved.
- Vault service validation now checks fresh Agent session binding, live SID mapping, exact Windows session ID, matching Agent thumbprint, active user/workstation, exact `ApplicationId`, exact `UserSessionId` parent, application registration, and existing permission/Vault policy at grant and redemption.
- Windows Agent caller inspection now rejects missing/untrusted Authenticode signatures, derives signer thumbprint, compares pipe and process token SID and integrity level, and obtains executable path/session/PID from Windows. This signature result is not yet compared to the server application registration because Agent-to-API registration lookup is not wired.
- Added migration `20260915075324_AgentWindowsIdentityAndApplicationBindings`, audit events with SID digests rather than raw SIDs, and security tests for mismatched session/binding IDs, missing user-session context, cross-tenant context, and expired binding.

## Trust chain

| Link | Status | Evidence / remaining gap |
|---|---|---|
| Windows SID → Windows Session | PARTIAL / NOT WINDOWS-HOST VALIDATED | Agent source derives these from Windows; actual service/pipe impersonation has not been run on a Windows service account. |
| Agent → Organization / Workstation | IMPLEMENTED SERVER-SIDE | Existing Agent mTLS authentication derives the workstation and organization from the certificate binding. Agent private-key ownership is still unverified. |
| Windows SID → NexorSys User | IMPLEMENTED SERVER-SIDE | Admin-only workstation-scoped identity mapping; no SID/username fallback. |
| Agent observation → UserSession | IMPLEMENTED SERVER-SIDE / NOT AGENT-INTEGRATED | Agent mTLS route requires one active, recent session for the mapped user/workstation. Agent does not yet call it. |
| Process → trusted application | PARTIAL | Authenticode trust and signer extraction are implemented in the Windows host; comparison to registered publisher/root and real Windows verification are pending. |
| Application → ApplicationSession → UserSession | IMPLEMENTED SERVER-SIDE | Stable Application and UserSession foreign keys are required by Vault authorization. Legacy/null links fail closed. |
| ApplicationSession → permission → Vault | PARTIAL / RELEASE DISABLED | Existing server authorization is retained and extended; endpoint hard gate prevents credential release until all local trust boundaries are proven. |
| Agent → approved credential-delivery adapter | NOT IMPLEMENTED | No application adapter exists; no secret is delivered. |

## Tests and verification

- Identity solution + Windows Agent build: passed, 0 errors; 13 existing nullable-analysis warnings remain.
- Security test suite with a disposable PostgreSQL 16 database: **20 passed, 0 failed, 0 skipped**. Includes EF migration on a fresh database and Vault grant/redeem/replay/concurrency/re-authorization with the new Windows binding checks.
- EF pending-model-change check: no pending model changes.
- PostgreSQL integration: tested against a temporary local PostgreSQL 16 container; container stopped and removed after test.
- Windows-host integration: **not run**. No service installation, named-pipe ACL/impersonation, WinVerifyTrust, certificate-store/key ACL, or real service-token test is claimed.
- External validation / penetration testing: not performed.

## Remaining blockers

- Agent certificate store enrollment, private-key exclusivity/ACL, secure expected-thumbprint provisioning, and mTLS HTTP client integration.
- Windows Agent must call session bind/end endpoints using only Windows-derived SID/session data; the local IPC caller cannot supply these as authority.
- Windows service installation under a dedicated restricted non-admin service SID, and Windows-host tests for service, ACL, impersonation, process tokens, session extraction, Authenticode, and certificate access.
- Compare actual signed process path/publisher against the registered application policy, verify application registration/activity and ApplicationSession in the Agent flow, and test adversarial/cross-tenant cases end to end.
- Credential-delivery adapter, Vault API enablement, and application-specific integration. API credential release is intentionally disabled.
- Production Data Protection, LDAPS, customer PKI, Credential Provider/Winlogon, NFC/CPS/e-CPS readiness, code signing, commercial installer, customer deployment, and penetration testing remain outside this completed slice.

## Operational notes

- Session-binding lease is 45 seconds. Windows logoff/session-change must call the mTLS end-session route; if it cannot, the lease expires and authorization stops. User lock/deactivation, Identity logout/expiry, application-session end, workstation revocation, mapping revocation, and Agent certificate mismatch are rechecked or deny at authorization time.
- `ApplicationSession` links are nullable for legacy compatibility, but release authorization rejects missing links. Existing legacy sessions need not be backfilled for the product to remain safe; they simply cannot authorize release.
- The app registration endpoint accepts only delivery mechanism `none`; registration records do not imply that executable verification or credential delivery is operational.
