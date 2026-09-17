# NexorSys Identity + Kiosk — Phase 4 Implementation Report

Date: 2026-09-15

## Outcome

**NOT READY.** Backend Agent-certificate identity and one-time Vault release are implemented and internally exercised, but end-to-end trust is incomplete: there is no Windows Agent, named-pipe IPC, OS process/user binding, or approved application delivery adapter. Complete authorization coverage also remains open. No external validation occurred.

## Implemented in this slice

- Added explicit workstation enrollment states: `ENROLLED`, `PENDING_APPROVAL`, `ACTIVE`, `REVOKED`, `REPLACED`, and `DECOMMISSIONED`, with approval/enrollment/validation/revocation/decommission timestamps and replacement linkage.
- Added tenant-scoped administrator endpoints to request enrollment, approve and associate a CA-issued certificate thumbprint, revoke, decommission, and replace workstations. Certificate thumbprints have a database uniqueness constraint across tenants to prevent transfer/rebinding.
- Added a certificate proof-of-possession endpoint. It only activates an approved enrollment after the presented TLS client certificate chains to a trusted OS CA, is valid, has Client Authentication EKU, and matches the pre-associated certificate thumbprint. This uses the OS trust store; no CA is implemented or fabricated here.
- Production Kiosk/Fleet certificate authentication now requires the workstation to be active in both the legacy flag and lifecycle state, with exact thumbprint binding. Successful validation records `LastValidatedAt`.
- Vault authorization now also rejects a workstation not in lifecycle state `ACTIVE`.
- Revocation, replacement, and decommission close active user and application sessions for the workstation.
- Added EF migrations. Existing workstation records are assigned `PENDING_APPROVAL` on upgrade and consequently fail closed until re-enrollment and proof. Full migration history was applied to a fresh disposable PostgreSQL 16 test database; no customer/production database was touched.
- Changed workstation hostname uniqueness to be organization-scoped (organization + hostname), while certificate thumbprints remain globally unique. The hostname migration preserves tenant semantics; no production data migration was run.
- Added lifecycle negative tests, including Vault denial for revoked devices.
- Added a distinct Agent-certificate binding on an already-active workstation. An administrator approves the external Agent certificate thumbprint; a second certificate proof-of-possession is required before the server recognizes that Agent identity. Agent and Kiosk certificates are not interchangeable.
- Added an mTLS-only `/api/agent/v1/vault-release` surface. It derives organization/workstation from the distinct Agent certificate rather than request identity fields.
- Added 20-second opaque one-use grants. Only a SHA-256 token hash is stored. Redemption uses a serializable transaction, conditionally consumes the grant once, re-evaluates authorization at redemption, decrypts only after consumption, and returns credentials only on the dedicated mTLS Agent route with `Cache-Control: no-store`.
- Release authorization checks organization, active/unlocked user, recent user session, active workstation and proven Agent certificate, application and non-read permission, matching recent application session, and active/unexpired Vault entry scoped to the application and user. Concurrent serialization conflicts fail closed. Grant creation, consumption, denial and mapped Agent-authentication failures are audited without secret material.
- Added a PostgreSQL integration test for fresh migrations, protected storage, successful one-time redemption, replay denial, concurrent redemption (at most one winner), and permission removal after grant creation. Fixed an older `department_policies.id` migration to explicitly cast valid UUID strings and stop on malformed identifiers.

## Tested / built

- Security unit tests: **12 passed, 0 failed**; the PostgreSQL integration test is skipped unless `NEXORSYS_TEST_POSTGRES` is set.
- PostgreSQL 16 integration test using a disposable local container: **1 passed, 0 failed**, including fresh-schema migration, one-use/replay behavior, concurrent redemption, and permission revocation. The disposable container was removed after the run.
- Identity solution clean rebuild: **succeeded, 0 errors, 13 nullable-analysis warnings** in existing LDAP/repository/Kiosk/API paths. The final incremental build also succeeded.
- EF migration generation used a design-time-only placeholder connection string; the migration integration test separately applied the full history to an isolated PostgreSQL database.
- EF reports no model changes pending after the generated migrations.
- NuGet vulnerability audit: no vulnerable packages reported.
- No Windows Agent service or named-pipe server, IPC integration test, approved-app delivery adapter, Credential Provider integration, commercial installer, or external PKI test was built in this slice.

## Release gate

| Area | Status | Evidence | Remaining blocker |
|---|---|---|---|
| Authentication | PARTIAL | Phase 3 paths and 11 focused tests | Provider/hardware ceremonies and Windows sign-in unvalidated |
| Authorization | NOT COMPLETE | Existing policies and Vault decision checks | Canonical User→Group→Role→Permission evaluation not applied to every endpoint |
| Tenant isolation | PARTIAL | EF filters and selected cross-tenant tests | Endpoint-wide integration/IDOR matrix on PostgreSQL |
| Sessions | PARTIAL | Server session checks; device lifecycle closes bound sessions | Cross-component revocation/integration tests |
| Device trust | IMPROVED | mTLS chain + thumbprint + active lifecycle check | Real CA, certificate deployment and revocation infrastructure |
| Device enrollment | IMPLEMENTED, NOT EXTERNALLY VALIDATED | Admin request/approval, certificate PoP, revoke/replace/decommission API | CA policy, customer provisioning workflow, database migration validation |
| Agent | SERVER IDENTITY ONLY | Distinct Agent certificate approval/proof and mTLS API; no Agent executable | Dedicated least-privilege Windows service, private-key ACL, OS caller attestation |
| IPC | NOT IMPLEMENTED | No localhost HTTP auth path re-enabled | Named-pipe ACL/authentication, protocol, replay and Windows identity tests |
| Vault | AUTHORIZATION + GRANT | PostgreSQL-backed checks and mTLS one-use redemption; no ordinary browser REST secret | Real Agent transport and application-specific delivery |
| Credential release | SERVER PATH IMPLEMENTED; END-TO-END NOT IMPLEMENTED | Dedicated mTLS route returns only after serializable re-authorization and grant consumption | Agent/IPC, Windows user/process binding, app integration, external mTLS |
| Application launch | PARTIAL | Server app-session authorization and Kiosk launch bounds | Publisher/path policy and app integration |
| AD/LDAP | PARTIAL | Existing integration | Strict LDAPS cert validation, stable IDs, sync/disable/group tests |
| NFC | PARTIAL | BCrypt PIN verification; UID not treated as proof | Reader/device ceremony and real hardware validation |
| CPS/e-CPS | FAIL CLOSED | Stub validation returns false | Provider implementation and issuer testing |
| Credential Provider | ENGINEERING ONLY | Native code builds; auth disabled | Agent integration, signed package, Winlogon tests |
| Installer | NOT IMPLEMENTED (COMMERCIAL) | Existing unsigned engineering-only Credential Provider MSI | Product installer, service setup, rollback/upgrade/uninstall |
| Upgrade/uninstall | NOT COMPLETE | Existing limited package source | Registry/service cleanup and rollback validation |
| Database | PARTIAL | EF migration generated | PostgreSQL upgrade/rollback/backup/restore tests |
| Data Protection | PARTIAL | Vault uses ASP.NET Data Protection | Production key persistence/protection configuration |
| HTTPS/mTLS | SOURCE CONTRACT | Certificate chain/usage/thumbprint validation | Deployed TLS terminator/client cert and production exercise |
| Audit | PARTIAL | Lifecycle actions audited; certificate proof audit shares DB save | Full audit-event coverage and integrity/retention validation |
| Logging | PARTIAL | New lifecycle records omit cert and secret material | Repository-wide sensitive-log review |
| Frontend | PARTIAL | Phase 3 test/build evidence | Device lifecycle UI and error-state integration |
| Tests | PARTIAL | 12 security unit tests + 1 PostgreSQL release integration test | API/mTLS/IPC/installer end-to-end security matrix |
| Documentation | PARTIAL | This report and Phase 4 protocol/lifecycle notes | Deployment, installer, authorization and release procedures |
| External validation | NONE | No external evidence | PKI, hardware, Winlogon, penetration and customer deployment validation |

## Legacy/release inventory (no deletion performed)

| Classification | Current examples | Release treatment |
|---|---|---|
| KEEP | `User-Administration/backend`, `User-Administration/kiosk-nfc` | Canonical source; package only after release allowlist review |
| REFERENCE / LEGACY | Root `Pinede.NFC.APP`, old product presentations/docs, old Credential Provider scripts | Exclude from product payload; retain for engineering history until owners approve removal |
| BUILD ARTIFACT / ENGINEERING ONLY | `kiosk-windows-auth/build-phase3`, old MSI/WiX outputs, `bin`, `obj`, frontend `dist` | Exclude from commercial package; do not install unsigned outputs |
| CUSTOMER DATA / SENSITIVE | `User-Administration/backups/*.sql`, `scratch/users_dump.json`, seed/export files | Never package or distribute; retention and lawful deletion/review is an owner decision |
| REVIEW BEFORE DISTRIBUTION | historical scripts/configs and legacy docs | Scan for secrets/customer data before source/archive publication |

No broad legacy cleanup was performed. In particular, historical data files remain in the workspace and are not release inputs.

## Not implemented in this slice

- Authenticated Windows Agent service, versioned named-pipe IPC, Windows SID/process/session binding, and local replay protection.
- Concrete application-specific credential delivery. Server-side mTLS release is implemented, but deployment must remain blocked until the Agent exclusively owns the private key and an approved app adapter is integrated.
- One centralized authorization evaluator across every API endpoint and the requested endpoint-by-endpoint classification/testing.
- Production PostgreSQL, Data Protection persistence, LDAPS integration, directory group sync, installer lifecycle, Authenticode signing, and external security validation.

## External dependencies

Customer CA/PKI and certificate issuance/revocation; production PostgreSQL and protected Data Protection key store; Windows code-signing certificate; customer AD/LDAPS and authoritative Windows SID mapping; real Windows Agent private-key ACL and named-pipe tests; supported hardware/readers and identity provider accounts; per-application credential delivery integrations; independent penetration and Windows security review.
