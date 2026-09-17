# NexorSys Identity + Kiosk — Phase 5 Final Security Report

**Classification: NOT READY**  
**Assessment scope:** `User-Administration` repository only; source/worktree state dated 2026-09-15. This is a software engineering assessment, not a production certification.

## 1. Executive summary

Phase 5 materially improved tenant scoping, ownership checks, signed license entitlement enforcement, executable identity checks, audit redaction, fail-closed placeholders, frontend chunking, and release packaging. The final integrated local test run passed 47 automated tests (44 security/backend, including one PostgreSQL-backed integration test; 3 Windows Agent), with zero failures/skips. Frontend passed 7/7. .NET Release and Kiosk builds passed with zero warnings/errors; frontend has no chunk-size warning. An unsigned native Credential Provider MSI and an unsigned Identity/Kiosk engineering ZIP were built.

The product is **NOT READY** because authorization coverage is not yet exhaustive per endpoint/resource/action; the WPF Kiosk launcher deliberately refuses executable launches until an end-to-end Agent authorization handshake is implemented; the signed revocation snapshot has no distribution/update service; production Data Protection ACL/restart behavior is unverified; and no signed commercial MSI/package was produced. The engineering ZIP is not a commercial release.

## 2. Starting state

Phase 4 baseline reported 35 backend/security, 3 Windows, and 7 frontend tests passing, PostgreSQL integration enabled, zero skipped, zero-warning Release/Kiosk builds, a 578.49 KB frontend chunk warning, signature verification without complete entitlement enforcement, and an unsigned engineering package. Phase 5 expanded policy and tests and reduced the frontend largest chunk below 500 KB.

## 3. Blockers addressed

- Tenant-bound reads and writes were added or tightened across users, devices, workflows, kiosk sessions, PIN reset, fleet/workstation, audit, Windows authentication, and related operations. Caller-supplied tenant/user/machine labels are not authorization proof.
- Sensitive credential identifiers were removed from selected API/log/audit paths; centralized audit redaction was added and historical credential-like audit values are cleared by migration.
- User/workstation creation paths use signed license entitlement checks; signed claims, product/org/version, validity interval, feature modules, limits, revocation and monotonic revocation sequence are verified server-side.
- Executable policy now requires an approved canonical path, file SHA-256, Authenticode-verified publisher identity and server registration. Arbitrary URL/protocol execution remains denied.
- Fake NFC CUID writes, ANS pairing/declaration success and legacy Windows-auth routes were disabled; reset, restore and unsupported federation operations fail closed. Vault release remains disabled.
- Installer scripts that directly wrote Credential Provider registry state were disabled. MSI build output is isolated and refuses non-empty output directories; WiX now consumes the DLL from the selected build output.
- Build artifacts are path-mapped to `/src`, and package scanning now inspects text and binary content for credential-shaped material and absolute customer/personal paths.

## 4. Authorization matrix

See `docs/phase5-authorization-matrix.md` in this report and package. High-risk controller families were reviewed and representative cross-tenant, ownership, role, session, workstation, Agent and Vault assertions are present. The matrix is not an exhaustive generated OpenAPI/action map. Exhaustive GET/POST/PUT/PATCH/DELETE A→B coverage for every resource, plus groups/roles/permissions/site selection/MFA/FIDO2/emergency-access routes, remains incomplete; several such capabilities have no complete backend route implementation. This gate is not closed.

## 5. License enforcement

Central `LicenseEntitlementGuard` re-verifies the persisted signed token and a signed ECDSA revocation snapshot for product, organization, version, expiry, revocation, modules, user count and workstation count. Database cache edits cannot raise signed limits. User and workstation entry points consult the guard. Negative tests cover tampering, expiration, wrong key, revocation sequence, feature and limit denial. Concurrent seat-limit stress/race testing and end-to-end entitlement review of every feature entry point are not complete.

## 6. Executable trust

Agent trust binds organization/application registration to canonical path, file hash and Authenticode publisher. Unit/security cases deny wrong path, traversal, hash, publisher, and unregistered identity. URLs/protocols are denied. The WPF launch service currently denies all launches while the Agent validation handshake is not wired end to end. No arbitrary executable launch is enabled; commercial Kiosk app-launch functionality is therefore incomplete.

## 7. Session ownership

Kiosk sessions and Agent grants bind organization, user, workstation, application, Windows session and lifecycle state; one-time Vault grant redemption rechecks current permissions. PostgreSQL integration exercises wrong org/workstation/binding, expired proof, locked user, revoked permission and concurrent replay. A complete per-endpoint session ownership table and all requested negative permutations remain outstanding.

## 8. Audit and log security

Central audit redaction masks password/PIN/badge/CPS/FIDO/token/secret/credential/key-like values; selected routes no longer log raw NFC identifiers or client machine names. Historical migration clears credential-like audit columns. This is improved coverage, not a verified exhaustive review of every application log, exception, telemetry sink, and resource disposal path.

## 9. Secrets and configuration

The engineering package passed the implemented binary/text scan for private-key blocks, credential assignments, JWT-shaped values, personal absolute paths and the known customer path patterns. No signing key or production credential was created or rotated. JWT-shaped test material was present in prior repository history; working-tree cleanup does not erase Git history or copies. Rotation and history-handling steps are documented in `docs/phase5-security-operations.md`. Rotation and hosting-provider history purge require the credential/repository owners.

## 10. Data Protection

Production startup requires durable key-ring/encryption configuration and fails closed when required configuration is missing. Operational ACL/certificate/restart checks are documented. No customer service identity, production certificate, protected key-ring directory, multi-node persistence, or restart/decryption validation was available; production persistence/ACL gate remains open.

## 11. Database and startup

Release packaging successfully generated an idempotent EF migration script. The integrated test runner started a disposable PostgreSQL 16.9 container and applied migrations for its PostgreSQL-backed agent/Vault test. Customer upgrade/rollback, backup/restore, production schema size/performance and multi-node migration orchestration were not validated. API restore and reset routes now return 501 without writing/executing uploaded data.

## 12. Frontend

Frontend tests: 7 passed, 0 failed, 0 skipped. Production build succeeded without the former 578.49 KB warning; largest emitted JS asset was 345.03 kB. `npm audit --omit=dev --audit-level=high` reported 0 vulnerabilities. Frontend chunk splitting does not establish application security or production browser compatibility.

## 13. Agent and device trust

Windows Agent IPC validates caller image hash, Authenticode signature/publisher, named-pipe identity and server-side workstation/application bindings. Agent tests: 3/3. The PostgreSQL security test covers one-time/replayed grant behavior and ownership. Customer PKI issuance, certificate rotation/revocation distribution, service ACLs, deployed Agent upgrade and real workstation trust remain unvalidated.

## 14. Credential Provider

Native x64 Credential Provider DLL and MSI compiled successfully in an isolated output directory. Direct-install helper scripts now refuse registry mutation outside the MSI workflow. The resulting MSI is unsigned engineering output and was not installed or exercised at Winlogon. Existing historical MSI binaries in the repository/worktree were not overwritten or deleted; release packaging excludes them.

## 15. Packaging

`scripts/build-release.ps1` produced an unsigned ZIP containing API, Agent, Kiosk, frontend, migration SQL, documentation, SPDX SBOM, manifest and checksums. Manifest explicitly states `signed=false` and `commercialRelease=false`; native MSI is excluded. Source worktree was dirty, so this was an engineering build, not a clean-checkout release.

## 16. Signing

`scripts/Sign-NexorSysArtifact.ps1` verifies a current Code Signing EKU certificate, signs PE/MSI artifacts with SHA-256 and HTTPS RFC3161 timestamp, then verifies signatures. No authorized commercial certificate/private key, timestamp endpoint or `signtool.exe` was available; no artifact was signed. The signed MSI release criterion remains open.

## 17. Test inventory

- `Nexorsys.Identity.SecurityTests`: 44 tests, includes one PostgreSQL-backed integration case in the integrated runner.
- `Nexorsys.WindowsAgent.Tests`: 3 tests.
- Frontend Vitest: 7 tests.
- Kiosk: Release compile only; no dedicated Kiosk automated test project in this run.
- Native Credential Provider: x64 compile + unsigned WiX MSI build; no installation/Winlogon test.

## 18. Exact final test results

- Backend/security: **44 passed / 0 failed / 0 skipped**.
- Windows Agent: **3 passed / 0 failed / 0 skipped**.
- PostgreSQL integration: **1 passed / 0 failed / 0 skipped**, included within the 44 security tests (not an additional test).
- Frontend: **7 passed / 0 failed / 0 skipped**.
- Kiosk tests: **0 / not available**; compile passed.
- Combined .NET tests: **47 passed / 0 failed / 0 skipped**.

## 19. Build results

Backend solution Release `--no-restore -warnaserror`: 0 warnings, 0 errors. Kiosk Release `--no-restore -warnaserror`: 0 warnings, 0 errors. Native Credential Provider x64 + WiX MSI compile succeeded; a first isolated attempt under the OS Temp directory emitted MSB8029 and exposed a stale WiX source path, both addressed by using a non-Temp isolated directory and parameterized DLL path. Final native build emitted no compiler warnings. Frontend build had no chunk-size warning.

## 20. Artifact audit

The final Identity/Kiosk engineering ZIP contained 216 files. No forbidden config/debug/source-map/backup/old-MSI file names were found. Binary/text scan found no prohibited secret pattern or personal/customer absolute path. `SBOM.spdx.json` present; manifest contains 214 artifact entries and says unsigned/non-commercial; 215 checksum entries all verified. Largest JS chunk: 345.03 kB. This is a scoped pattern scan, not a guarantee that every possible secret format is detectable.

## 21. Remaining software blockers

1. Complete exhaustive authorization/action matrix and systematic A→B tests, including currently absent product route families and inactive/revoked/expired coverage.
2. Wire Kiosk executable launch to the Windows Agent/server authorization handshake; currently all launch attempts fail closed.
3. Implement and deploy authenticated revocation snapshot distribution/update/monitoring; local signature verification alone has no feed transport.
4. Complete entitlement coverage and concurrent user/device/workstation limit race tests across every creation path.
5. Complete whole-system audit/log/resource ownership review and Kiosk automated test coverage.
6. Run a canonical clean Git checkout restore/build/test/package pipeline; this run used a dirty user worktree with explicit engineering override.
7. Obtain authorized signing infrastructure and produce/verify the signed commercial MSI and signed release package; validate installer upgrade/rollback.
8. Verify production Data Protection ACL, certificate, persistence, restart and multi-node behavior.
9. Address the known historical secret exposure through owner-led credential rotation and repository-history handling.

## 22. External validation required

Real customer PKI/mTLS enrollment and revocation; AD/LDAPS; NFC reader/card and CPS/e-CPS hardware; FIDO2; Windows Credential Provider installation and Winlogon behavior; customer applications and publisher certificates; production Data Protection identity/ACL and restart; backup/restore drills; independent penetration test. These are not claimed as passed.

## 23. Known limitations

Vault credential release remains disabled by design. Federation, NFC writing/pairing, ANS declaration, reset and restore workflows are not represented as functional where the secure integration is absent. Historical Nexorsys namespaces/internal compatibility identifiers remain in source and binary assembly names; customer-facing packaging is renamed, and selected historical MSI artifacts remain in the source worktree but are excluded from the release ZIP. The source tree is broadly dirty with unrelated user edits; no destructive cleanup was performed.

## 24. Final classification

**NOT READY** — substantial Phase 5 software controls and verification are in place, but the unresolved software blockers above preclude “READY FOR EXTERNAL VALIDATION.” No production-readiness, compliance, penetration-test, signed-release, hardware, Winlogon, or customer infrastructure claim is made.
