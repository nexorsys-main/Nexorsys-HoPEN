# NEXORSYS PHASE 3 IMPLEMENTATION REPORT

Date: 2026-09-15

## IMPLEMENTED

- Selected `User-Administration/kiosk-nfc/Pinede.NFC.APP.csproj` as the one canonical production Kiosk. Labeled the root `Pinede.NFC.APP` as legacy reference, renamed its assembly, and made its portable publisher refuse to publish.
- Removed the canonical Kiosk's old local administrator/settings UI and simulated badge-authentication control. No simulated NFC scan can be triggered by the commercial UI.
- Added fail-closed production device trust at the API boundary: production Kiosk/Fleet routes require a currently valid, revocation-checked client certificate whose thumbprint is bound to an active workstation in the selected organization. Production no longer accepts the shared API key. Development compatibility is isolated to Development.
- Configured Kestrel HTTPS endpoints to request client certificates. The Kiosk reads its enrolled client key from the Windows CurrentUser\My store, requires HTTPS outside Test/Debug, requires organization/workstation UUIDs, disables redirects, and never silently falls back to localhost HTTP.
- Removed the universal native emergency badge/PIN, disabled unauthenticated Credential Provider localhost auth/reset/session calls, and removed hostname/badge-only server session creation. These flows fail closed until the authenticated Agent contract exists.
- Corrected the NFC provider to verify BCrypt PIN hashes and reject inactive/locked accounts. PSC and e-CPS token stubs no longer report successful authentication; provider-local fake validation returns false.
- Tightened launch/session handling: Kiosk launch first requests a server-side application-session authorization, rejects non-HTTPS URLs and EXEs outside existing Program Files roots, starts desktop applications without shell argument parsing, and requests application-session close on process exit. Kiosk logout clears local identity/application state and revokes the server session; fabricated fallback tokens were removed.
- Tightened Vault authorization to require an active Vault entry/application, unexpired user permission, active/unlocked user, active workstation, current user session, and matching active application session. REST remains decision-only and never returns an application password.
- Added production startup checks for database/JWT/origin configuration, exact configured CORS origins, HTTPS redirect, generic global exception responses, session idle activity refresh, and fixed-time API-key comparison where Development compatibility is used.
- Removed badge UIDs from routine Kiosk trace logging and removed the exposed diagnostic popup. Removed source-embedded database passwords from helper scripts and redacted known demo client-secret values from historical SQL snapshots; the snapshots still contain customer data and must not be distributed without review.
- Updated the WiX package to exclude the obsolete WPF Kiosk and autorun entry, renamed the package branding, and made the MSI build script repository-relative and non-installing.
- Updated the native build to use a fresh `build-phase3` CMake directory and fixed a wide-character-to-UTF-8 conversion warning.
- Added executable tests for BCrypt NFC PIN behavior and fail-closed PSC validation; expanded Vault tests to require organization, user, application, workstation, and session context.
- Updated the threat model and this report. Aligned the .NET 8 EF/Npgsql dependencies to compatible same-major patch lines (`Microsoft.EntityFrameworkCore` 8.0.31 and Npgsql EF provider 8.0.11); no major-version migration was attempted.

## TESTED

- Backend security tests: 5 passed, 0 failed, 0 skipped.
- Frontend Vitest: 1 passed, 0 failed, 0 skipped.
- A live PostgreSQL migration/integration run was not possible because no production/test database endpoint is configured.

## BUILT

- Identity .NET solution: built successfully with 0 warnings and 0 errors.
- Canonical WPF Kiosk: built successfully with 0 warnings and 0 errors.
- React/Vite production bundle: built successfully; Vite reports a large JavaScript chunk.
- Native Windows Credential Provider DLL: configured and built with Visual Studio Build Tools 18 / MSVC 19.51 / Windows SDK 10.0.26100; build output is in `User-Administration/kiosk-windows-auth/build-phase3`.
- Unsigned engineering MSI: built at `User-Administration/kiosk-windows-auth/build-phase3/NexorSysIdentityCredentialProvider.msi`. It packages the Credential Provider only, not the legacy WPF Kiosk. Do not install or distribute it.
- A commercial Identity + Kiosk installer/uninstaller is not present in the current source tree; the existing MSI is only a Credential Provider package.

## EXTERNALLY VALIDATED

NONE.

## EXTERNAL VALIDATION REQUIRED

- Production HTTPS deployment, PostgreSQL migration/upgrade/backup/restore, production Data Protection key persistence, and load/rate-limit behavior.
- Customer CA/PKI trust, client-certificate enrollment/issuance, renewal/rotation, revocation, private-key ACLs and real mTLS traffic. The application-side certificate validation contract exists; the provisioning authority does not.
- A signed Credential Provider and authenticated Agent service/IPC protocol, service identity/ACLs, real Windows Credential Provider/Winlogon behavior, shared Windows-user isolation, and hardware-backed key behavior. Native Provider authentication is intentionally disabled until this is implemented.
- Real NFC/CPS/CPx/e-CPS/Pro Santé Connect/FIDO2 hardware and provider ceremonies; NFC UID alone remains an identifier, not cryptographic proof.
- Real AD/LDAP/LDAPS TLS validation, stable directory identifiers, synchronization/disable propagation, group/role mapping and retry behavior.
- Per-customer applications, signing/publisher allowlists and application-specific launch/credential-use integration. The trusted Agent credential-release channel is not implemented, so Vault credentials cannot yet be delivered to apps.
- Independent penetration test and Windows security review.

## KNOWN RESIDUAL RISKS

- Authorization is not yet one complete User→Group→Role→Permission calculation across every API. The endpoint-wide authorization and tenant/IDOR tests requested in the brief have not been implemented; only selected paths have focused tests.
- Workstation certificate enrollment, site ownership/approval/replacement workflows, certificate revocation APIs, and a managed device lifecycle are incomplete.
- The Identity/Kiosk production stack cannot be launched end-to-end from this workspace because production DB, HTTPS, CA and enrolled device certificate inputs are intentionally absent.
- Several legacy/reference artifacts and historical SQL backups remain in the workspace. The known demo client-secret values were redacted and source-embedded helper-script database passwords removed; backups may still contain customer data and require archive review before any source or backup distribution.
- Existing native UI/protocol and legacy namespaces retain compatibility identifiers. They are not evidence of commercial branding, but require a signed-package and registry upgrade/uninstall review.
- The frontend production bundle exceeds Vite's 500 kB chunk advisory threshold. Identity and Kiosk builds have no compiler warnings or errors in the final recorded runs.
- The stale generated `kiosk-windows-auth/build/CMakeCache.txt` contains an obsolete external absolute path. A fresh clean `build-phase3` was used successfully, but cleanup of the old generated build directory was blocked by the workspace's destructive-operation guard.

## COMMERCIAL RELEASE STATUS

NOT READY FOR EXTERNAL VALIDATION
