# NexorSys Identity + Kiosk — Phase 4 P0.3 Report

## Release decision

**NOT READY FOR EXTERNAL VALIDATION.** Vault credential release remains hard-disabled. Both Agent grant/redeem routes return `503 RELEASE_NOT_CONFIGURED`; the Windows Agent also returns `RELEASE_NOT_CONFIGURED` after identity and application checks. No credential-delivery adapter is configured or exercised.

## A. Implemented

- The Windows Agent loads only machine-level, non-secret API URL and certificate-thumbprint settings. It locates the unique matching LocalMachine\My certificate and fails closed for invalid URL/thumbprint, missing/mismatched/expired certificate, missing Client Authentication EKU, invalid online chain, inaccessible CNG key ACL, or failed private-key sign/verify proof. No key material is copied to config or exposed over IPC.
- The Agent HTTP client uses HTTPS, manual client-certificate selection, no redirects, revocation checking, and bounded request timeouts. It has no API-key, JWT, cookie, localhost, or other downgrade path.
- The pipe protocol is version 2 and contains only request metadata plus `ApplicationSessionId` as a locator. Strict JSON rejects unknown fields; timestamps must be UTC `Z`; length, operation, lifetime, version, and replay are bounded/checked. The server derives organization/workstation from the client certificate.
- The Agent derives caller SID, session ID, PID, executable path, token SID/integrity, Authenticode trust, and publisher thumbprint from Windows. It checks session active/unlocked before processing, calls authenticated binding and application-session validation, checks response binding, and fails closed on API or identity mismatch.
- Server binding refreshes the existing exact SID/session identity binding with a 45-second lease, rechecks active mapping and active user session, and audits create/refresh/end/denial without recording raw SID (a digest is used in binding audit values). Application-session validation checks the current binding, user/session, tenant, workstation, app session, active app registration, publisher/install-root, and current permission.
- A five-second WTS poll revokes observed sessions when they are no longer active/unlocked; shutdown attempts authenticated revocation. Observed-session tracking is capped. WTS x64 union alignment was corrected and covered by a Windows host test.
- Added a deployment script that requires an Authenticode-valid published executable in a location not writable by ordinary users, a non-loopback HTTPS API URL and certificate thumbprint, and configures the dedicated virtual service account, restricted service SID, delayed automatic start, recovery actions, service ACL, and protected registry ACL. It deliberately does not start the service; PKI/key provisioning is an external prerequisite.
- The HTTP/API boundary and named-pipe/Credential Provider are not treated as proof that credential delivery works. Credential Provider integration and commercial installer remain out of scope.

## B. Windows-host validation

| Boundary | Result | Evidence / limitation |
|---|---|---|
| Windows platform build | PASS | Full solution build: 0 warnings, 0 errors. |
| WTS session state query | PASS | Current interactive Windows session reported active/unlocked; invalid IDs fail closed. |
| Authenticode positive case | PASS | WinTrust accepted installed signed .NET host and returned signer thumbprint. |
| Authenticode negative case | PASS | Unsigned test assembly was rejected. |
| Installer script syntax | PASS | Parsed on this Windows host. |
| Actual Agent service installation/start/stop/restart/service identity/SID | NOT RUN | Workspace Agent executable is unsigned. Service is not installed. |
| Named pipe client ACL, impersonation and unauthorized-client runtime exercise | NOT RUN | Agent service is not running; no live pipe was exercised. |
| Certificate-store lookup / Agent private-key ACL / signing as service identity | NOT RUN | No enrolled Agent certificate/private key with the required service-only DACL is provisioned. Source implements startup checks only. |
| Agent mTLS to a live Identity API | NOT RUN | No enrolled Agent certificate/server endpoint is available for this host run. |
| Live server bind → application-session validation chain | NOT RUN | HTTP contract and server logic implemented; no cert-backed API-to-Agent end-to-end run. |
| Application registration and process policy | PARTIAL | Server enforces registered root/publisher against Agent-observed image; local Authenticode primitive was tested, but an approved production app/signing certificate was not available. |
| Service recovery and shutdown revocation | NOT RUN | Deployment and shutdown behavior are implemented but service lifecycle was not exercised. |

## C. Automated tests

- `Nexorsys.Identity.SecurityTests`: **22 passed, 0 failed, 0 skipped**, run against a fresh disposable PostgreSQL 16 container; container removed after completion.
- `Nexorsys.WindowsAgent.Tests`: **3 passed, 0 failed, 0 skipped**, executed on Windows.
- Full solution build: **0 warnings, 0 errors**.
- Deployment PowerShell parse: **PASS**.

These tests establish targeted invariants and Windows API primitives, not the complete installed-service trust chain.

## D. Trust chain status

| Link | Implementation | Tested |
|---|---|---|
| Windows process → PID/path/token SID/integrity | IMPLEMENTED | PARTIAL — local signed/unsigned Authenticode cases tested; live pipe caller extraction not run. |
| Windows SID | IMPLEMENTED | NOT TESTED end-to-end — derived via pipe impersonation in code. |
| Windows session | IMPLEMENTED | PARTIAL — WTS current-session state tested; session extraction through a running service/pipe not run. |
| Named pipe | IMPLEMENTED | NOT TESTED at runtime — ACL and impersonation code exist; live authorization/concurrency tests not run. |
| Agent process | IMPLEMENTED | PARTIAL — compiles and Windows unit tests pass; service not installed or started. |
| Agent certificate/private key | IMPLEMENTED, fail-closed | NOT TESTED — no enrolled cert or service identity key access. |
| Organization | IMPLEMENTED | NOT TESTED end-to-end — server derives from authenticated Agent certificate mapping. |
| Workstation | IMPLEMENTED | NOT TESTED end-to-end — server derives from authenticated Agent certificate mapping. |
| WindowsUserIdentityBinding | IMPLEMENTED | PARTIAL — server lookup/deny logic is covered by source/security tests, not live mTLS request. |
| AgentWindowsSessionBinding | IMPLEMENTED | PARTIAL — 45-second create/refresh/end flow; DB/Vault invariants tested, Windows-to-server call not run. |
| NexorSys UserSession | IMPLEMENTED | PARTIAL — server rechecks active user/session/lock/expiry predicates; no live Agent call. |
| Application process | IMPLEMENTED | PARTIAL — Authenticode local primitive and path/publisher policy tests; no approved app identity on host. |
| Application registration | IMPLEMENTED | NOT TESTED end-to-end — server registration/validation logic exists, no cert-backed Agent call. |
| ApplicationSession | IMPLEMENTED | NOT TESTED end-to-end — linked IDs and server verification exist; no live API chain run. |
| Permission | IMPLEMENTED | PARTIAL — server checks permission at validation; PostgreSQL release tests verify current-permission recheck at Vault service boundary. |
| Vault | IMPLEMENTED but disabled | PARTIAL — grant/redeem invariants tested in PostgreSQL; API routes and Agent return `RELEASE_NOT_CONFIGURED`; no secret delivery. |

## E. Remaining blockers

- **Service/key trust:** signed Agent release artifact, actual service installation, restricted service identity validation, service-only CNG key ACL provisioning and proof of private-key use while running as that identity.
- **Windows integration:** live pipe ACL/impersonation and authorized/unauthorized client tests; PID/SID/session mismatch, lock/logoff/user-switch, replay, malformed and concurrent-request tests against a running service; service recovery and shutdown revocation tests.
- **End-to-end:** Agent certificate enrollment, mTLS handshake against deployed Identity API, successful/denied binding and application-session validation, certificate rotation/revocation, and cross-tenant requests using real certificates.
- **Application verification:** real registered application signed by the enrolled approved publisher and installed under the registered root; replacement/renaming and alternate-path runtime tests.
- **Vault / delivery:** approved credential-delivery adapter and a later explicitly authorized phase to connect it. Release remains disabled.
- **Production readiness:** production PostgreSQL and migration rollout, durable Data Protection key storage, endpoint-wide authorization review, production PKI/LDAPS, code signing and commercial installer, Credential Provider/Winlogon, NFC/CPS/e-CPS validation, penetration testing, and customer deployment remain outstanding.

## Release controls

- `VaultCredentialReleaseEnabled` remains `false` in `AgentController`.
- Agent IPC has no success/release path; it stops at `RELEASE_NOT_CONFIGURED`.
- The engineering deployment script is not a commercial installer and was not executed.
- No end-to-end credential delivery is claimed.
