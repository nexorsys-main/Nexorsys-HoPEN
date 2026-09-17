# NexorSys Identity + Kiosk — Phase 2 Implementation Report

Date: 2026-09-15

## IMPLEMENTED

- Added server-side CSRF enforcement for browser-session mutations and frontend CSRF header propagation.
- Added server-backed portal sessions with `sid` claims, one-hour expiry, logout revocation, active-user checks, idle checks, and forced invalidation when a user is inactive or locked.
- Closed organization-selection ambiguity during login: first-time provisioning requires an organization context, and an account already belonging to another organization cannot be reprovisioned through a client-selected organization.
- Removed mock audit fallback and changed audit writes to derive actor and organization from authenticated server context instead of accepting a client-supplied `AuditLog` entity.
- Hardened Windows-auth mutation endpoints with admin authorization and preserved workstation/org checks.
- Hardened kiosk badge/PIN/SSO entry points with enrolled active workstation binding (`X-Organization-Id`, `X-Workstation-Id`, matching machine name).
- Added workstation binding to kiosk sessions and a migration for the new field.
- Added application authorization checks before application-session creation and organization/active-session checks before application-session stop.
- Removed the plaintext temporary-PIN API response; secure PIN delivery now fails closed until an out-of-band channel exists.
- Changed federation placeholders to explicit 501 responses; no fake provider token or successful callback is emitted.
- Added executable backend security tests for cross-tenant filtering, IDOR-style resource denial, vault binding, and ciphertext storage.
- Added Vitest + jsdom frontend test infrastructure and an executable CSRF test.
- Fixed the `/noise.svg` frontend build warning with a real public asset.
- Replaced the canonical kiosk’s fixed AES key/IV with Windows DPAPI current-user protection.
- Removed customer-specific application defaults from the canonical kiosk configuration.
- Rebranded canonical kiosk UI, startup registration, installer, uninstaller, executable/configuration labels, and custom protocol to NexorSys naming.
- Added a threat model covering browser, tenant, vault, kiosk, workstation, native provider, LDAP, and launch threats.
- Removed the remaining plaintext legacy credential/test artifacts and changed EF design-time configuration to fail when `NEXORSYS_DATABASE` is absent.

## TESTED

- Backend security tests: 3 passed.
- Frontend Vitest tests: 1 passed.
- React production build: passed; unresolved `/noise.svg` warning fixed. Vite still reports the pre-existing large bundle warning.
- Backend solution build: passed, 0 errors.
- Canonical nested WPF kiosk: passed, 0 errors.
- Root legacy/reference WPF kiosk: passed, 0 errors.
- Installer build: passed, 0 errors.
- Uninstaller build: passed, 0 errors.
- Repository credential scan: no runtime credential literals found; `pk_live_` in the UI is only a generated-key prefix.

## BUILT BUT NOT HARDWARE-VALIDATED

- Native credential-provider build was attempted, but the existing CMake cache points to an obsolete `D:/Innovera/...` path and the required Visual Studio 2022 BuildTools instance is unavailable. No native build or Winlogon success is claimed.

## EXTERNALLY VALIDATED

- None in this environment. No claim is made for real hardware or production infrastructure.

## EXTERNAL VALIDATION REQUIRED

- PostgreSQL clean/legacy migration execution and backup/restore.
- Real AD/LDAP/LDAPS certificate validation, stable directory identifiers, disable propagation, and group synchronization.
- NFC/CPS/CPx/FIDO2 ceremony and lifecycle hardware tests.
- mTLS/device-certificate replacement for the current compatibility API-key transport.
- Windows Credential Provider, DPAPI/TPM, signed binaries, Winlogon, ACL/IPC, and shared-workstation cleanup.
- Production HTTPS/CORS, penetration testing, application launch allowlists, and real customer application integration.

## KNOWN RESIDUAL RISK

- Legacy namespaces and reference directories still contain compatibility-era `Pinede` identifiers. They are not the canonical product branding, but a final repository rename would require coordinated project/installer migration.
- Several existing nullable/static-analysis warnings remain.
- The current kiosk trust path is materially stronger than hostname/localhost trust, but should be replaced by certificate-authenticated mTLS/device enrollment before production deployment.
- The legacy WPF admin screen remains in the recovered kiosk code but has no default credential and refuses empty-secret authentication; delegated tenant-scoped authorization should replace it in a future kiosk-only hardening pass.
