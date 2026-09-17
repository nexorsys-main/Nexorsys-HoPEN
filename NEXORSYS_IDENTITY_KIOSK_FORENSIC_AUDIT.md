# NEXORSYS IDENTITY + KIOSK
# EXISTING PROJECT FORENSIC AUDIT

Audit date: 2026-09-15  
Scope: `C:\Users\nexor\Desktop\Nexorsys`  
Method: source/configuration/documentation inspection; no source, project, schema, branding, dependency, or deployment changes were made. Generated artifacts were treated as evidence, not source of truth.  
Evidence convention: “NOT VERIFIED IN SOURCE” means no implementing code or test evidence was found. “SIMULATION / MOCK — VERIFY IMPLEMENTATION” means a code path exists but is explicitly simulated, fallback-only, or contradicted by the implementation. “EXTERNAL VALIDATION REQUIRED” means hardware, Windows policy, directory, provider, or deployment evidence is required.

## 1. Executive Summary

The recovered project is a Pinede-branded healthcare IAM and kiosk prototype/working system composed of:

- A .NET 8 ASP.NET Core API in `User-Administration\backend`, split into Core, Infrastructure, and API projects.
- A React/Vite administrative web UI in `User-Administration\frontend\pinede-identity-ui`.
- Two materially similar WPF kiosk generations: `Pinede.NFC.APP` at the workspace root and `User-Administration\kiosk-nfc`.
- A native C++ Windows Credential Provider under `User-Administration\kiosk-windows-auth`, plus MSI/DLL/certificate artifacts.
- PostgreSQL/EF Core migrations, Docker files, SQL scripts, PowerShell deployment/maintenance scripts, documentation, backups, and demo/presentation assets.

The valuable foundation is real: a working-shaped API/UI separation, PostgreSQL persistence, BCrypt PIN hashes, PC/SC card-reader integration, WPF MVVM kiosk flows, SignalR plumbing, Windows Credential Provider source, migrations, audit/session/workstation concepts, and application-permission filtering.

The system is not commercially reusable or security-ready as inspected. The dominant blockers are:

1. There is no `Organization`, `Tenant`, `Site`, or `OrganizationId` in the domain model or schema. The current data model is single-tenant.
2. Configuration contains plaintext database, LDAP, SMTP, kiosk-admin, API-key, license, and JWT-secret material; Docker configuration also contains a plaintext LDAP password.
3. `Auth:MockMode` is enabled in the checked-in API configuration. Several UI screens intentionally fall back to demo data.
4. The browser stores JWT and user data in `localStorage`; the kiosk stores API keys in editable configuration and the native provider stores session JWT material in `HKLM`.
5. Several endpoints have missing or commented authorization, including fleet operations and Windows-auth mutation operations. Tenant/workstation authorization is not consistently enforced.
6. The Windows credential provider uses a hardcoded application secret, registry blobs rather than DPAPI/TPM-backed storage, and sends HTTP to `localhost`; the source itself notes an incomplete user mapping.
7. FIDO2/WebAuthn, e-CPS, PSI, smart-card identity, federation, ANS, and application auto-login are represented by models/providers/UI/documentation, but end-to-end implementation and validation are not demonstrated.
8. There are no backend test projects and only two frontend test files; no meaningful Windows integration/security/end-to-end evidence was found.

Recommended disposition: KEEP the product concept, visual language, API/UI/kiosk workflow, and hardware integration seams; REFACTOR the identity/security/data boundaries; REPLACE unsafe secret/session/credential-provider mechanisms; ADD explicit tenant/org/site modeling, production authentication, vault design, device trust, authorization policy enforcement, tests, and deployment controls; REMOVE or isolate Pinede-only seed/configuration/assets after Phase 2 decisions.

## 2. Repository Structure

### Root

- `Pinede.NFC.APP\`: standalone/root WPF kiosk generation, net8.0-windows, plus installers and a prebuilt portable publish.
- `User-Administration\`: main recovered workspace.
- No repository `.git` directory was present at the inspected root; `git status` therefore could not be used to establish history or baseline.

### Main workspace components

| Area | Path | Observed contents |
|---|---|---|
| Backend solution | `User-Administration\backend\Pinede.Identity.Solution.sln` | Core, Infrastructure, API only; no test projects |
| Backend API | `...\backend\src\Pinede.Identity.API` | Program, 17 controllers, middleware, filter, SignalR hub, services, background services, config |
| Domain | `...\backend\src\Pinede.Identity.Core` | `Entities.cs`, DTOs, abstractions, JWT settings |
| Persistence/infrastructure | `...\backend\src\Pinede.Identity.Infrastructure` | EF Core/Npgsql context, repositories, services, migrations |
| Admin frontend | `...\frontend\pinede-identity-ui` | React/Vite UI, 15 routes/pages, Axios, SignalR services, tests |
| Empty/generated alternate UI | `...\frontend\nexorsys-identity-ui` | Only `.vite`, `dist`, `node_modules`, `.env.local` observed; no source/package manifest in the inspected listing |
| WPF kiosk generation A | `User-Administration\kiosk-nfc` | WPF MVVM NFC kiosk and local services |
| WPF kiosk generation B | root `Pinede.NFC.APP` | Similar WPF MVVM kiosk with diagnostics, license and SignalR services |
| Native Windows auth | `User-Administration\kiosk-windows-auth` | C++ Credential Provider, PC/SC reader, crypto helper, CMake, WiX/MSI/DLL/cert artifacts |
| Deployment | `docker-compose*.yml`, Dockerfiles, `nginx.conf`, MSI/WiX scripts, publish/signing scripts | Development/packaging deployment paths |
| Database | `db\`, root SQL, EF migrations, backups | PostgreSQL schema scripts, seeds, manual fixes, backups |
| Scripts | `scripts\` | Setup, validation, backup, reset, deployment, monitoring, test helpers |
| Documentation | root/workspace `.md`, HTML presentations | Architecture, deployment, compliance, technical/functional presentations |
| Assets | `Assets`, frontend/presentation assets, logos/icons/certificates | Pinede logos, UI images, native provider certificate, binaries |

### Project files

- `backend/src/Pinede.Identity.API/Pinede.Identity.API.csproj`: .NET 8 Web SDK.
- `backend/src/Pinede.Identity.Core/Pinede.Identity.Core.csproj`: .NET 8 class library.
- `backend/src/Pinede.Identity.Infrastructure/Pinede.Identity.Infrastructure.csproj`: .NET 8 class library.
- `kiosk-nfc/Pinede.NFC.APP.csproj`: WPF/WinForms, net8.0-windows.
- root `Pinede.NFC.APP/Pinede.NFC.APP.csproj`: WPF/WinForms, net8.0-windows.
- root installer projects: `PinedeNFC.Installer.csproj`, `PinedeNFC.Uninstaller.csproj`.
- native C++ project generated under `kiosk-windows-auth\build`; source is CMake-based, not part of the .NET solution.

## 3. Complete Architecture

The source-derived architecture is:

```text
React/Vite Pinede Identity UI
  | Axios REST + SignalR; JWT in localStorage
  v
ASP.NET Core .NET 8 API :5000/:7000
  | controllers, policies, API-key filter, SignalR hub
  +--> EF Core/Npgsql --> PostgreSQL PinedeIdentity
  +--> LDAP/Active Directory
  +--> background revocation/retention services
  +--> mock/provider abstractions (NFC, Windows, OAuth, PSI/e-CPS)

WPF Pinede NFC kiosk (two generations)
  | PC/SC reader -> badge UID
  | API key + REST -> kiosk endpoints
  | optional SignalR bridge
  | local config/logs/registry startup
  v
Managed applications: URLs and local executable paths

Windows Credential Provider DLL
  | Winlogon/ICredentialProvider + PC/SC
  | local registry encrypted blob
  | WinHTTP localhost:5000 Windows-auth callback
  v
Windows logon / LSA credential submission
```

Trust boundaries:

- Browser to API: bearer JWT; browser storage is script-readable.
- Kiosk to API: shared API key; no demonstrated per-workstation certificate on the WPF path.
- API to database: connection-string credentials.
- API to LDAP: service account credentials.
- Native Credential Provider to Winlogon/LSA: privileged Windows logon boundary.
- Native provider to registry: `HKLM\SOFTWARE\PinedeIdentity\Sessions` and provider enrollment blobs.
- API to managed application: application metadata/paths are server-controlled but launch is local.
- Admin UI to settings/system endpoints: sensitive configuration is editable and transmitted.

The current system is a combination of Identity backend/admin UI, WPF kiosk, and native Windows logon component. It is not a single Windows service. A Windows background service was not verified in source; `BackgroundServices` found are ASP.NET hosted services.

## 4. Identity Application

Identity is represented by the backend/API plus the React admin UI. Implemented areas include users, role strings, direct user permissions, applications, authentication providers, audit, workflows, device registry, policies, federation records, kiosk sessions, workstations, and feature/license-shaped entities.

There is no first-class organization/site/group aggregate. “Group” is mostly a policy target/comment or directory concept. Roles are a string on `User`; authorization policies map role names in `Program.cs`.

The API exposes user CRUD/search, login, device management, audit, workflow, settings/system operations, federation, policy, monitoring, fleet, Windows-auth, ANS delegation, kiosk and health endpoints. The UI exposes all of these as navigation items, but several screens are fallback/demo or partially wired.

## 5. Kiosk Application

The WPF kiosk is a standalone desktop application using MVVM, XAML views, PC/SC, `HttpClient`, local JSON configuration, local logging, and an application launcher. It is dependent on the Identity API for identify/PIN/session/application data. It is not shown to be a complete Windows shell replacement or a Windows service.

Observed flow:

1. PC/SC reader detects a card and derives a UID.
2. Kiosk calls `POST /api/kiosk/identify` or `identify-mie` with badge/device identifier and machine name.
3. Kiosk calls `POST /api/kiosk/validate-pin` with user ID, PIN, badge/NFC UID, and machine name.
4. API validates BCrypt PIN, creates a kiosk session, and returns a session token plus authorized managed applications.
5. Kiosk displays/launches configured applications.
6. Logout/expiry/session cleanup are represented by service/view-model methods and API routes, but end-to-end enforcement and process teardown require validation.

The root and nested WPF projects are near-duplicates with divergent versions/features. This creates a material release ambiguity: source does not identify one canonical commercial kiosk build.

## 6. Windows Components

- WPF kiosk: user-mode desktop application, startup registry support, tray icon, PC/SC reader, API client, launcher.
- Native Credential Provider: C++ DLL implementing Windows credential-provider interfaces; source, generated build, DLLs, WiX/MSI packages, certificate and install scripts are present.
- ASP.NET hosted background services: inactive-user revocation and retention policy; not Windows services.
- No verified Windows service executable, service installer, named pipe, or local broker was found.
- Native provider calls `WinHttpConnect(..., L"localhost", 5000, ...)` and posts `/api/windows-auth/session-created` after Windows logon.
- Native provider writes JWT/session material to `HKLM\SOFTWARE\PinedeIdentity\Sessions` keyed by username.
- Native provider enrollment blobs are stored in registry; exact ACL and DPAPI protection must be verified from the omitted helper portions and installation scripts.

## 7. Backend/API

### Controllers and base routes

| Controller | Route | Key operations |
|---|---|---|
| AuthController | `/api/auth` | login, JWT issuance |
| UsersController | `/api/users` | list/search/create/update/delete/lock and badge-related user operations |
| KioskController | `/api/kiosk` | identify, identify-mie, validate-pin, validate-sso, session/application operations |
| DevicesController | `/api/devices` | MIE/device register/list/status/certify/delete |
| AuditController | `/api/audit` | read, write, delete/clear audit data |
| WorkflowController | `/api/workflows` | workflow list/create/status |
| FederationController | `/api/federation` | providers/policies/federation operations |
| NfcManagementController | `/api/nfc-management` | NFC administration |
| PinResetController | `/api/pin-reset` | PIN reset request/approval paths |
| PolicyController | `/api/policy` | department policy CRUD |
| SettingsController | `/api/settings` | settings read/write |
| SystemController | `/api/system` | backup/restore/system actions |
| FleetController | `/api/fleet` | workstation list/trust/heartbeat |
| MonitoringController | `/api/monitoring` | stats/events |
| WindowsAuthController | `/api/windows-auth` | enroll/validate/provider health/revoke/re-enroll/session callbacks |
| AnsDelegationController | `/api/ans-delegation` | NFC/RPPS pairing and logs |
| HealthController | `/api/health` | health/status |

This is an inventory from attributes in source; exact route suffixes are in each controller and should be generated into a signed API contract in Phase 2.

### Security observations

- JWT bearer and role policies are configured in `Program.cs`.
- KioskController is protected by `ApiKeyAuthAttribute` and a rate limiter, but the shared key is configuration-based.
- Fleet authorization attributes are visibly commented out in `FleetController`.
- WindowsAuthController has no controller-level `[Authorize]` or device-auth filter in the inspected source; several write operations accept identifiers/certificates in request bodies.
- `KioskController` returns exception messages including `ex.Message` and `ex.StackTrace` in 500 responses.
- Identify and PIN paths log badge identifiers, machine names, PIN length, and user IDs. API audit strings also embed identifiers.
- Many routes use direct `Guid` lookup without a tenant scope because no tenant scope exists.
- A full IDOR/authorization proof requires endpoint-by-endpoint tests; the code is sufficient to classify this as a high-risk unresolved area, not as safe.

## 8. Frontend

The active source UI is React with Vite, React Router, Axios, SignalR, Tailwind-related packages, Ant Design, Recharts, Framer Motion, i18next, and Lucide icons. The package manifest reports React 19/Vite 8-era dependencies, while repository documentation describes older React/Vite/Tailwind versions; the documentation is stale relative to the manifest.

Routes/screens:

`/login`, `/`, `/users`, `/workflows`, `/kiosk`, `/kiosk/pin-resets`, `/audit`, `/settings`, `/profile`, `/help`, `/federation`, `/devices`, `/monitoring`, `/fleet`, `/policies`.

Navigation is defined in `components/Layout.jsx`. The existing visual language is a light slate/white layout with emerald accents, cards, tables, forms, and a sidebar. The audit recommendation is to preserve it as requested.

Authentication uses `localStorage` keys `pinede_token` and `pinede_user`; Axios attaches the bearer token and removes it on 401. Protected-route checks are client-side and therefore not an authorization boundary.

Partial/demo indicators:

- `Federation.jsx` populates PSI/e-CPS fallback rows on API error and comments “Stub for demo”.
- `PolicyManagement.jsx` populates sample departments/policies on error and comments “Stub for demo”.
- `MonitoringDashboard.jsx` has a demonstration fallback.
- Several UI services and screens use hardcoded defaults, direct console logging, or fallback content.
- `Settings.jsx` handles database/LDAP/SMTP/API-key/admin-password material in browser form state and logs settings during save.

Accessibility/responsiveness: source inspection shows standard labels and semantic controls in places, but no automated accessibility suite, keyboard/focus audit, responsive test evidence, or screen-reader validation. Therefore production accessibility and responsive behavior are NOT VERIFIED IN SOURCE.

## 9. Database

Database engine: PostgreSQL via Npgsql/EF Core. Docker declares PostgreSQL 15; documentation mentions PostgreSQL 14+ and also says PostgreSQL 18 is recommended. The actual deployed version is NOT VERIFIED IN SOURCE.

### Main entity/table groups

- `users`: AD identity, profile, role, password hash, badge/CPS/FIDO identifiers, PIN lock state, Windows enrollment fields.
- `user_pins`: badge UID and BCrypt PIN hash.
- `applications`: name, client ID/secret, redirect URIs, active flag.
- `user_permissions`: user/application/permission-level grants and expiry.
- `audit_logs`: actor/resource/action, old/new values, IP/user-agent, timestamp.
- `kiosk_sessions`: user, token, badge/NFC UID, expiry.
- `user_sessions`, `workstation_sessions`, `session_events`, `application_sessions`.
- `workstations`, `user_workstations`, `credential_provider_fleets`.
- `identity_providers`, `federation_tokens`, `migration_phases`, `authentication_policies`, `user_devices`.
- `pin_reset_requests`, `authentication_events`, `department_policies`, `feature_flags`, `organization_licenses`, `ans_delegation_logs`, `workflows`.

### Entity relationship overview

```text
User 1--* UserPin
User *--* Application through UserPermission
User 1--* KioskSession / UserSession / AuthenticationEvent / AuditLog / UserDevice
User *--* Workstation through UserWorkstation
Workstation 1--1 CredentialProviderFleet
UserSession 1--* SessionEvent
Workstation 1--* WorkstationSession / UserSession / ApplicationSession
IdentityProvider 1--* AuthenticationPolicy
User 1--* FederationToken / Workflow / PinResetRequest / AnsDelegationLog
```

There is no `Organization` or `Tenant` root and no organization foreign key on users, applications, workstations, credentials, sessions, audit events, or policies. `OrganizationLicense` is a standalone license record, not a tenant boundary.

## 10. Authentication

- Local admin/user password login: `AuthController` verifies a stored BCrypt hash when present.
- LDAP/AD: `LdapService` and `ActiveDirectoryProvider` use configured LDAP/AD credentials and direct directory bind/search patterns. Deployment correctness, LDAPS/certificate validation, and directory ACLs are NOT VERIFIED IN SOURCE.
- JWT: API issues HMAC JWTs using `Jwt:Key`; checked-in key is a placeholder. Token revocation/refresh-token architecture was not found.
- Kiosk: API key + badge/MIE identification + BCrypt PIN; API returns a session token.
- Windows: native Credential Provider submits Windows credentials and calls API callbacks; server-side certificate/workstation checks exist in `WindowsAuthController`, but controller authentication and complete binding are incomplete.
- OAuth/provider resolver classes exist, but successful external-provider protocol evidence is NOT VERIFIED IN SOURCE.

## 11. MFA

The product concept is badge/NFC + PIN. `UserPin`, `KioskController`, `KioskService`, WPF authentication screens, lockout counters, and rate limiting provide a real implementation-shaped path. This is not sufficient evidence of production MFA assurance because badge UID is treated as an identifier, device binding is weak/inconsistent, and the kiosk API key is shared.

MFA methods other than NFC/PIN are mostly provider/model/UI scaffolding. No independent second-factor policy engine with verified enrollment/recovery/revocation was established.

## 12. NFC

PC/SC integration is real in both WPF generations (`NfcService.cs`) and in native `SmartCardReader.cpp`. UID acquisition includes an APDU `FF CA 00 00 00` strategy and a smart-card provider fallback. The backend matches identifier fields to user/device records.

Limitations:

- UID is not proof of possession of a cryptographically protected credential; cloning/UID replay risk is not addressed in the inspected path.
- No hardware test evidence or reader/card matrix was found.
- Mock/simulation paths exist in documentation/configuration.
- NFC is not verified against CPS/CartePS semantics.

Status: 🟡 PARTIAL IMPLEMENTATION; EXTERNAL VALIDATION REQUIRED.

## 13. FIDO2

FIDO2/WebAuthn appears in comments, entity fields (`FidoId`, `UserDevice.DeviceType`), provider names, feature flags, documentation, and UI concepts. No WebAuthn challenge/response library, registration ceremony, assertion verification, origin/RP-ID validation, attestation handling, or credential public-key storage was found.

Status: ⚠️ PROTOTYPE/PLANNED; NOT VERIFIED IN SOURCE.

## 14. Smart Cards / CPS

PC/SC and Windows CryptoAPI code can enumerate readers and derive a UID/provider container reference. `ECpsProvider`, `PsiProvider`, and `CartePS`-shaped models exist. This is not proof that CPS/e-CPS/CartePS authentication works: certificate-chain validation, PIN/signature verification, certificate mapping, revocation/OCSP, ANS/PSI protocol completion, and physical card testing were not verified.

Status: 🟡 PARTIAL/SCAFFOLD; EXTERNAL VALIDATION REQUIRED.

## 15. Federation

`IdentityProvider`, `FederationToken`, `AuthenticationPolicy`, `FederationService`, `FederationController`, and a React federation screen exist. Provider types include PSI, e-CPS, OAuth, and OpenID Connect. The UI explicitly supplies offline fallback demo data. Token references are modeled, but secure token exchange/validation and live provider interoperability are NOT VERIFIED IN SOURCE.

Status: ⚠️ PROTOTYPE/EXTERNAL INTEGRATION; EXTERNAL VALIDATION REQUIRED.

## 16. Directory / AD / LDAP

LDAP configuration includes `ldap://localhost:389`, Pinede-specific base DN/service account values, and a service password placeholder. Docker includes an internal IP/domain and plaintext service password. `LdapService` supports lookup/import/group-like checks; `ActiveDirectoryProvider` provides an authentication-shaped adapter.

No reliable evidence of LDAPS-only enforcement, certificate pinning/validation, secret rotation, connection pooling policy, least-privilege account, directory outage behavior, or production AD integration tests was found.

## 17. Applications

The managed application model/configuration supports IDs, names, descriptions, icons, URLs, and local executable paths. Current examples include Pinede-specific healthcare applications:

- EMED URL
- HESTIA local/domain URL
- SIGEMS local executable path
- BLUE KANGO URL
- a `test.com` demo entry

Backend permission filtering exists in `KioskController.GetAuthorizedManagedAppsAsync`/related service code. Kiosk launch is local `ProcessStartInfo`/URL launch. Universal credential injection or application auto-login was not verified. There is no generic per-application integration contract with protocol, logout, secret scope, rotation, or audit semantics.

Status: 🟡 PARTIAL; launch metadata/authorization exists, credential automation is NOT VERIFIED IN SOURCE.

## 18. Vault / Credentials

There is no dedicated vault service or vault-backed secret abstraction in the inspected architecture.

Observed secret flows:

- User passwords are accepted by API login and LDAP bind; stored local password hashes use BCrypt where present.
- User PINs are sent from kiosk to API and compared to `UserPin.PinHash`; PIN hashes are persisted.
- `Application.ClientSecret` exists as a database field.
- Native provider encrypts an AD password blob locally with AES-GCM using a PBKDF2 key derived from PIN + badge UID + hardcoded secret. The code comments identify a hardcoded `APP_SECRET`; storage is registry-based, not demonstrably DPAPI/TPM-backed.
- Native provider passes decrypted AD password into the Windows credential-provider logon flow.
- Admin settings expose LDAP/DB/SMTP/API-key/kiosk-admin fields in the React UI and POST sensitive values to settings/system endpoints.
- JWTs are stored in browser `localStorage` and native registry session values.

High-risk findings: API/database DTOs can expose broad `User` objects; `IdentifyResponse` contains a `User`; `LoginResponse` contains a `User`; settings and logs can carry secrets; no field-level secret classification or redaction policy was found.

## 19. Sessions

Session entities include `KioskSession`, `UserSession`, `WorkstationSession`, `ApplicationSession`, and `SessionEvent`. Kiosk sessions contain a `SessionToken` and expiry; unified user sessions contain token, auth method, activity, and status.

JWT refresh-token rotation, server-side token revocation, token hashing at rest, audience separation by client, replay detection, device binding, and reliable logout invalidation were NOT VERIFIED. Frontend logout removes local storage only. Background revocation services exist but do not establish complete bearer-token invalidation.

## 20. Workstations

Workstations have hostname, department, IP, location, active flag, Windows-auth flag, last-seen, certificate thumbprint, machine SID, authentication mode, user associations, and credential-provider fleet status.

Heartbeat/trust and certificate-shaped endpoints exist. Fleet authorization is visibly commented out in source. Request-provided hostname, machine SID, department, certificate thumbprint, and API key are not sufficient alone unless validated against a trusted enrollment and protected transport. Cross-tenant workstation ownership is impossible to enforce because tenant ownership is absent.

## 21. Security

### Positive controls found

- BCrypt package and use for password/PIN verification.
- JWT bearer configuration and role policies.
- Kiosk endpoint rate-limiting attribute.
- ASP.NET error middleware and security headers in `Program.cs`.
- PC/SC isolation through reader libraries.
- AES-GCM/PBKDF2 use in native provider source.
- Audit and authentication-event entities.

### Material weaknesses

- Secrets in checked-in JSON/YAML/config and UI defaults.
- Mock authentication enabled in API config.
- Shared kiosk API key; editable client configuration.
- Browser JWT in `localStorage`.
- HTTP/localhost native callback and no demonstrated mutual TLS.
- Hardcoded native crypto secret; no demonstrated DPAPI/TPM binding.
- Missing/disabled authorization on fleet/Windows-auth operations.
- No tenant isolation.
- Sensitive values in logs/responses and exception details.
- Demo/fallback behavior can make unavailable features appear present.
- No verified rate limits for all login/administrative/reset routes.
- No verified CSRF strategy for cookie-based future use; current bearer model does not remove XSS risk.

Commercial status: UNSAFE until these are addressed and independently tested.

## 22. Audit

Audit sources include `AuditLog`, `AuthenticationEvent`, `SessionEvent`, service/controller calls, and frontend audit views. Captured fields include actor/user/resource/action, old/new values, IP/user-agent, workstation, provider, event type, result, reason, and timestamp.

Audit integrity is not demonstrated: records are ordinary mutable database rows; the API includes a delete/clear path; no append-only storage, hash chain, signing, WORM export, or privileged separation was found. Audit strings may include badge identifiers, machine names, and request details. Secret redaction is not consistently demonstrated.

## 23. Tenant Isolation

Status: ❌ MISSING.

The architecture cannot safely support Organization A and Organization B as separate security domains from the inspected code. There is no tenant/org entity, tenant claim, organization foreign key, database global query filter, tenant-aware repository, tenant-aware audit, or tenant-aware kiosk/workstation credential scope. Most endpoints use direct primary-key lookups. `OrganizationLicense` is standalone and does not provide isolation.

This is a structural commercialization blocker, not a UI rename task.

## 24. La Pinède-Specific Dependencies

| Area | La Pinède-specific? | Evidence | Commercialization impact |
|---|---|---|---|
| Branding | Yes | namespaces, assemblies, product/company metadata, logos, titles | Rename/brand abstraction across all deliverables |
| Database | Yes | `PinedeIdentity`, Pinede-named migrations/config/scripts | Tenant-neutral database/configuration required |
| Authentication | Yes | `PinedeIdentity` issuer/audience, Pinede LDAP base/service account | Externalize issuer, directory, provider config |
| Users | Yes | AD/domain fields, clinic seed data, French clinic defaults | Provisioning/import policy required |
| Organizations | Effectively single clinic | no organization entity; `OrganizationLicense` only | Add true tenant hierarchy |
| Kiosk | Yes | project/product names, API keys, local URLs, Pinede registry paths | Productized enrollment/device identity required |
| Applications | Yes | EMED, HESTIA, SIGEMS, BLUE KANGO, `pinede.emed.fr` | Replace with tenant-configured integrations |
| Workstations | Yes | department/location/hostname assumptions and Pinede callback paths | Enrollment and policy ownership required |
| Configuration | Yes | Pinede DB, LDAP, license, admin password, clinic email | Secure external configuration/secrets |
| API | Yes | namespaces/controllers/docs, issuer/audience, route behavior | Brand-neutral contracts and versioning |
| Frontend | Yes | package name, logo alt/title, French clinic text | Preserve visual language; externalize content |
| Deployment | Yes | Docker/container names, MSI names, certificate, scripts | Product packaging, signing, upgrade policy |

## 25. Branding Dependencies

Brand occurrences are present in namespaces and project names (`Pinede.Identity.*`, `Pinede.NFC.APP`), assembly/product/company/description metadata, Docker service/container names, database names, JWT issuer/audience, registry paths, native DLL/MSI/WiX names, certificates, README/documentation, UI package name, logo/alt text, browser/UI copy, scripts, URLs, and configuration.

The root kiosk is explicitly `PinedeIdentityTerminal`; nested kiosk product is `Pinede Kiosque NFC`; API product is `Pinède Identity API`; UI is `pinede-identity-ui`. The required future names are not implemented consistently. Phase 2 should inventory and rename only after package/upgrade/registry/API compatibility decisions.

## 26. Existing Tests

Found:

- Frontend `src/App.test.jsx` and `src/app/dashboard/Dashboard.test.jsx`.
- API `.http` request file.
- PowerShell scripts named `test-*`, login/auth/system scripts, kiosk API scripts, and README test instructions.
- No backend unit/integration test project, Windows Credential Provider test project, hardware test suite, database integration suite, or end-to-end suite was found.

The frontend manifest has build/dev/preview scripts but no `test` script. Actual test execution was not performed because the request prohibits modifying the codebase and the repository has no declared test command that could be run without creating/altering generated state. Therefore pass/fail is NOT VERIFIED.

## 27. Build / Runtime

- .NET SDK target: .NET 8 for API/core/infrastructure and net8.0-windows for WPF.
- A bundled `.dotnet` SDK 8.0.419/8.0.25 runtime artifacts exists under the root kiosk, but global runtime use is NOT VERIFIED.
- Frontend uses Node/npm-compatible Vite tooling; exact Node version is NOT pinned by an `.nvmrc`/package-manager declaration.
- PostgreSQL Docker image: 15; docs are inconsistent about 14+/18.
- API ports documented/configured around 5000 and 7000; frontend Docker maps 3005 to 3000.
- Commands documented: `dotnet restore`, `dotnet run`, `npm install`, `npm run dev`, `docker-compose up --build`, kiosk publish/sign scripts, CMake/WiX MSI scripts.
- Configuration is split among appsettings, custom settings, environment variables, Docker Compose, kiosk JSON, SQL scripts, and PowerShell scripts.
- No verified production reverse-proxy TLS configuration or secret injection mechanism was found.

No source build/test result is claimed in this report.

## 28. Dependencies

### .NET

CommunityToolkit.Mvvm 8.4.2; ASP.NET JWT bearer/Newtonsoft/OpenAPI/EF design 8.0.10; BCrypt.Net-Next 4.0.3; EFCore.NamingConventions 8.0.3; Npgsql EF Core 8.0.8; System.DirectoryServices.Protocols 8.0.0; System.DirectoryServices.AccountManagement 10.0.5; Microsoft.AspNetCore.SignalR.Client 10.0.8; Microsoft.Extensions.Hosting/Http 10.0.x; PCSC/PCSC.Iso7816 7.0.1; Newtonsoft.Json 13.0.4.

The API project mixes .NET 8 packages with 10.0.x packages in the root WPF project. Compatibility and support policy require verification.

### Frontend

React/React DOM 19.x, Vite 8.x, TypeScript 6.x, React Router DOM 7.x, Axios 1.15.x, SignalR 10.x, Ant Design 5.x, Tailwind 4.x plugins, i18next 26.x, Recharts 3.x, Framer Motion 12.x, Jest/testing-library packages. The lockfile exists for the Pinede UI. No automated vulnerability/license scan was run; vulnerable/abandoned status is NOT VERIFIED.

### Native/deployment

Windows SDK APIs, PC/SC, BCrypt, WinHTTP, CMake 3.15+, MSVC, WiX artifacts, MSI and certificate artifacts. Signing trust chain and certificate lifecycle are NOT VERIFIED.

## 29. Technical Debt

- Duplicate kiosk generations with overlapping namespaces/files/configuration.
- Domain model is a large flat entity file and mixes identity, federation, enterprise fleet, licensing, and workflow concerns.
- DTOs return domain entities directly in important paths.
- String-based role/provider/status/assurance values.
- Conflicting documentation versus manifest/configuration versions.
- Manual SQL scripts, backups, seed scripts, and EF migrations with unclear authoritative order.
- Empty `Class1.cs` placeholders and likely dead/demo code.
- Broad settings/system functionality mixed with identity API.
- Error handling and logging expose operational details.
- Frontend fallback data can mask backend failures.
- No explicit API versioning or contract generation.
- No reproducible lockfile/toolchain policy across all components.

## 30. Security Findings

| ID | Finding | Evidence | Severity |
|---|---|---|---|
| SEC-01 | Plaintext secrets in checked-in config | API appsettings/custom settings, Docker Compose, kiosk JSON, settings UI | Critical |
| SEC-02 | Mock authentication enabled by default | `appsettings.json`, `Auth.MockMode: true` | Critical |
| SEC-03 | No tenant isolation | Entities, migrations, AppDbContext have no tenant/org key/filter | Critical |
| SEC-04 | Browser bearer token in localStorage | frontend `api/index.js`, `Login.jsx` | High |
| SEC-05 | Shared/editable kiosk API key | kiosk `appsettings.json`, `ParametresApplication`, API key filter | High |
| SEC-06 | Windows-auth endpoints lack consistent authorization | `WindowsAuthController`; fleet auth comments | Critical |
| SEC-07 | Native hardcoded crypto secret and registry credential storage | `CryptoHelper.cpp`, provider source | Critical |
| SEC-08 | Native callback uses HTTP localhost and naive JSON parsing | `PinedeCredential.cpp` | High |
| SEC-09 | Sensitive data in logs/exception responses | KioskController logging and 500 response | High |
| SEC-10 | Audit rows deletable and not integrity-protected | `AuditController`, `AuditLog` | High |
| SEC-11 | FIDO2/PSI/CPS claims exceed verified implementation | providers/models/docs/UI fallbacks | High/product risk |
| SEC-12 | No demonstrated refresh/revocation/replay controls | JWT/session code and frontend logout | High |
| SEC-13 | Credential automation/vault absent as a product boundary | app model/config/native blob only | Critical |
| SEC-14 | Potential IDOR/direct primary-key access | multiple controllers; no tenant scope | High |

No exploit was executed; findings are source-based risk findings.

## 31. Commercialization Gap

### A. Branding changes

Replace Pinede namespaces, assemblies, package IDs, product metadata, registry keys, MSI/DLL/certificate names, UI titles/logos, Docker names, issuer/audience, docs, URLs, and assets with NexorSys Identity/Kiosk branding while preserving compatibility strategy.

### B. Architecture changes

Define a canonical Identity service and canonical Kiosk client; separate domain/application/infrastructure/security concerns; establish versioned APIs and explicit trust boundaries.

### C. Multi-tenant changes

Add organization/site/department hierarchy, tenant claims, tenant-aware database constraints/query filters/repositories/services/audit/credentials/workstations, and cross-tenant authorization tests.

### D. Identity changes

Define provider lifecycle, enrollment, recovery, account lifecycle, groups, roles, permissions, policy evaluation, directory sync, and provisioning contracts.

### E. Kiosk changes

Canonical client packaging, workstation enrollment, device identity, offline policy, lock/unlock/session cleanup, user switching, safe launch/termination, and update/rollback behavior.

### F. Security hardening

External secret management, secure cookies or equivalent token strategy, key rotation, TLS/mTLS, rate limits, anti-replay, redaction, safe errors, authorization review, and threat modeling.

### G. Vault changes

Dedicated application credential vault with envelope encryption, key custody, rotation/expiry, least-privilege retrieval, non-exportability where possible, and audited access.

### H. Windows integration changes

Credential-provider threat model, DPAPI/TPM/LSA design, signed binaries, secure IPC, Windows service/elevation model, ACL review, credential cleanup, and domain/offline behavior.

### I. Application integration changes

Tenant-configured adapters, launch policies, URL/executable allowlists, protocol-specific SSO contracts, logout, secret scope, and integration certification.

### J. Licensing dependencies

Clarify the future Admin boundary; make license state tenant-scoped and server-verifiable; review third-party/native/WiX/package licenses. Do not build Admin in this phase.

### K. Configuration changes

Environment-specific configuration schemas, secret references, validation, safe defaults, no checked-in secrets, and configuration auditability.

### L. Deployment changes

Signed installers, upgrade/uninstall semantics, supported OS matrix, TLS reverse proxy, backups/restore, migrations, monitoring, log retention, and disaster recovery.

### M. Testing requirements

Unit, API authorization/tenant, database, provider contract, hardware, Windows, kiosk, credential-provider, security, load, offline, upgrade, and end-to-end suites.

### N. Documentation

Replace claims with evidence-backed capability matrix; separate prototype/demo/external-validation statements; provide operator, installer, security, recovery, and integration docs.

### O. Production readiness

Independent security review, penetration test, key/certificate lifecycle, incident response, privacy/legal review, support model, SLA/telemetry, and release governance.

## 32. KEEP

- Core product concept: Identity + Kiosk.
- Existing visual language and navigation structure, subject to accessibility/security fixes.
- .NET API/Core/Infrastructure separation as a starting point.
- PostgreSQL/EF migration approach after schema governance is established.
- BCrypt PIN hashing and lockout concepts.
- PC/SC reader integration seams.
- WPF MVVM screens/services and application-selection workflow.
- Audit/session/workstation concepts as foundations.
- SignalR/eventing seam where threat-modeled and authorized.
- Native Credential Provider concept if redesigned and independently validated.

## 33. REFACTOR

- Domain model into bounded modules and explicit DTOs.
- Authentication provider contracts and policy engine.
- Tenant/org/site model and query boundaries.
- Authorization on every endpoint.
- Settings/configuration and secret handling.
- Session/token lifecycle.
- Audit integrity/redaction/retention.
- Kiosk duplication into one canonical project.
- Application integration model and launcher allowlist.
- Build/package/version/documentation consistency.

## 34. REPLACE

- Checked-in/static secrets and API-key-only kiosk trust.
- Hardcoded native crypto secret and registry-only credential protection.
- Browser localStorage bearer-token strategy if threat model requires stronger isolation.
- Direct secret-bearing settings endpoints/UI.
- Unauthenticated Windows-auth mutation paths.
- Demo fallbacks in production builds.
- Any unsupported claim of universal FIDO2/CPS/PSI/application auto-login.

## 35. ADD

- Tenant/org/site entities and isolation enforcement.
- Secure secret/vault service.
- Key rotation and token revocation/refresh design.
- Workstation enrollment and mTLS/device identity.
- Formal FIDO2/WebAuthn if in product scope.
- CPS/e-CPS/PSI protocol implementations and conformance tests if in scope.
- Application adapter/credential lifecycle model.
- Offline/timeout/session cleanup policy.
- Security/audit integrity controls.
- Comprehensive test and CI/CD/release-signing pipeline.

## 36. REMOVE

Phase 2 only, after compatibility review: Pinede-only branding/assets/configuration, clinic-specific URLs/apps/seeds, hardcoded demo/test applications, stale claims, duplicate obsolete kiosk generation, and exposed credentials. Do not remove current artifacts during this audit.

## 37. External Validation Required

- Physical NFC readers/cards and UID/replay behavior.
- CPS, e-CPS, CartePS, PSI/Pro Santé Connect interoperability.
- FIDO2/WebAuthn browser/authenticator ceremony.
- Active Directory/LDAP/LDAPS, group mapping, password change and outage behavior.
- Windows Credential Provider behavior at Winlogon, UAC, RDP, lock/unlock, fast user switching, offline/domain conditions.
- MSI installation, signing, certificate trust, upgrade/rollback/uninstall.
- Application launch/SSO/logout for EMED, HESTIA, SIGEMS, BLUE KANGO.
- Production PostgreSQL version/backup/restore/retention.
- Security assessment and penetration testing.

## 38. Open Questions / Decisions Required

1. Is the canonical kiosk the root `Pinede.NFC.APP` or nested `User-Administration\kiosk-nfc`?
2. Is the native Credential Provider a supported product component or an experiment?
3. Which authentication methods are in the initial commercial SKU: NFC/PIN only, CPS/e-CPS, FIDO2, PSI, AD, or combinations?
4. Must application credentials be injected, proxied, or only launch applications?
5. What is the tenant/site/department hierarchy and licensing boundary?
6. Is NexorSys Kiosk online-only, offline-capable, or fail-closed?
7. What Windows editions, domain states, hardware readers, and browsers are supported?
8. What are the legal/compliance claims actually intended, and what evidence is required before using them?
9. Which existing database/backups are authoritative?
10. Which Pinede integrations remain as reference adapters versus being removed from the commercial baseline?

## 39. Recommended Phase 2 Work

Phase 2 should begin with decisions and a security baseline, not broad UI redesign:

1. Freeze and select the canonical kiosk/backend/frontend sources.
2. Remove secret exposure from development/deployment paths and rotate every exposed credential/key.
3. Model tenant/org/site ownership and write authorization/IDOR tests before enabling commercialization.
4. Define Identity/Kiosk API contracts and a secure workstation enrollment protocol.
5. Redesign session/token/vault/Windows credential storage boundaries.
6. Separate production code from mock/demo/fallback behavior.
7. Build a capability matrix for NFC/PIN, CPS/e-CPS, FIDO2, federation, AD, and application integrations.
8. Add backend, frontend, hardware, Windows, and security test foundations.
9. Apply branding only after package, registry, installer, API, and upgrade compatibility is decided.
10. Preserve the current visual language while replacing clinic-specific content through configuration/localization.

## 40. Complete Feature Matrix

| Feature | Current Status | Location | Evidence | La Pinède-specific? | Commercial Gap | Priority |
|---|---|---|---|---|---|---|
| User directory | ✅ Fully implemented shape | `UsersController`, `UserDirectory.jsx`, `UserRepository` | CRUD/search/import-shaped code | Data/config yes | Tenant-aware lifecycle and tests | High |
| Organizations | ❌ Missing | Core entities/migrations | No Organization/Tenant entity or FK | Single-clinic assumption | Add tenant hierarchy/isolation | Critical |
| Sites | ❌ Missing | Core entities/migrations | No Site entity | Implicit location only | Add site ownership/policy | High |
| Departments | 🟡 Partially implemented | `User.Department`, `DepartmentPolicy` | String fields/policy table | Clinic departments/defaults | Normalize and scope by tenant | High |
| Groups | 🟡 Partially implemented | LDAP/service/policy target | No first-class group aggregate | AD-specific | Group sync/ownership | High |
| Roles | 🟡 Partially implemented | `User.Role`, Program policies | String role and role policies | Role names are local | Permission model and tenant scope | High |
| Permissions | 🟡 Partially implemented | `UserPermission`, Kiosk filtering | Direct user-app grants | App seeds/config | Group/policy inheritance and expiry enforcement | High |
| Password login | ✅ Implemented shape | `AuthController` | BCrypt/local + LDAP path | Pinede issuer/config | Production IdP/account lifecycle | High |
| JWT | 🟡 Partially implemented | `JwtService`, Program | Bearer issuance/config | Pinede issuer/audience | Rotation, refresh, revocation, storage | Critical |
| Refresh tokens | ❌ Missing/NOT VERIFIED | API/session code | No refresh-token entity/flow found | No | Add secure lifecycle | High |
| NFC badge identification | 🟡 Partial | WPF `NfcService`, KioskController | PC/SC UID + API lookup | Badge/config/clinic | Anti-cloning/device assurance/hardware tests | Critical |
| PIN validation | ✅ Implemented shape | `KioskService`, `UserPin`, KioskController | BCrypt compare and lock counters | Badge/config | Stronger factor binding/rate-limit tests | Critical |
| PIN reset | 🟡 Partial | PinResetController/UI/entity | Request/approval model | Clinic workflow | Secure recovery and tenant scope | High |
| MFA policy | 🟡 Partial | `AuthenticationPolicy`, provider services | Assurance/provider fields | Healthcare labels | Enforced factor policy | High |
| FIDO2/WebAuthn | ⚠️ Prototype/demo | provider/model/docs/UI | No ceremony/verification implementation | Healthcare roadmap claims | Implement or remove claim | Critical |
| CPS/smart card | 🟡 Partial | PC/SC/native reader/providers | Reader/UID/CryptoAPI paths | CPS claims | Certificate/signature validation | Critical |
| e-CPS | ⚠️ Prototype/demo | `ECpsProvider`, federation | Provider class/model only | Healthcare-specific | Live protocol validation | High |
| PSI/federation | ⚠️ Prototype/demo | FederationService/Controller/UI | Demo fallback; token model | Healthcare-specific | Provider contract/conformance | High |
| AD/LDAP | 🟡 Partial | LdapService/ActiveDirectoryProvider | Bind/search/import-shaped code | Pinede DN/account | LDAPS, outage, sync, tests | Critical |
| Application registry | ✅ Implemented shape | `Application`, settings/config | IDs, URLs, paths, permissions | EMED/Hestia/etc. | Tenant-owned integration catalog | High |
| Application launch | 🟡 Partial | kiosk `LanceurApplicationService` | Process/URL launch | Clinic paths | Allowlist, lifecycle, tests | High |
| Credential vault | ❌ Missing | No vault service | ClientSecret/registry blob only | Pinede apps | Dedicated vault and key custody | Critical |
| Credential injection | ⚠️ Prototype/demo | native provider + docs | Windows password submission only | AD/Pinede workflow | App-specific secure adapters | Critical |
| Credential rotation | ❌ Missing/NOT VERIFIED | models/config | No rotation workflow | No | Add rotation/expiry | High |
| Workstations | 🟡 Partial | Workstation/Fleet/WindowsAuth | Entities and endpoints | Hostnames/departments | Enrollment/trust/tenant scope | Critical |
| Workstation trust | 🟡 Partial | certificate/SID checks, FleetController | Request checks and heartbeat | Pinede fleet | mTLS/secure enrollment/authz | Critical |
| Windows credential provider | 🟡 Partial | native C++ source/MSI | Provider source/build artifacts | Pinede names/registry | Threat model, secure storage, tests | Critical |
| Kiosk sessions | 🟡 Partial | KioskSession/UserSession | Create/expiry/events | Single tenant | Revocation/replay/device binding | Critical |
| Lock/unlock | 🟡 Partial | WPF screens/view models | UI/session concepts | Kiosk workflow | OS-level enforcement validation | High |
| User switching | ❓ Cannot determine from code | WPF/native/API | Session entities, no full proof | Kiosk | End-to-end Windows tests | High |
| Timeout/session cleanup | 🟡 Partial | config/services/background services | Expiry/retention-shaped code | Clinic defaults | Fail-closed and cleanup tests | High |
| Offline mode | ⚠️ Simulation/mock | docs/config/mock mode | MockMode and “offline” claims | Pinede deployment | Explicit secure offline policy | Critical |
| Audit logs | 🟡 Partial | AuditLog/AuditController/AuditService | CRUD/events/UI | Clinic labels | Integrity, immutability, redaction | Critical |
| Security events | ✅ Implemented shape | AuthenticationEvent/SessionEvent | Event persistence | No | Correlation/monitoring/retention | High |
| Monitoring | 🟡 Partial | MonitoringController/UI | Stats/events/fallback | Pinede UI | Production telemetry/alerts | Medium |
| Licensing | ⚠️ Prototype/demo | OrganizationLicense, LicenseService, scripts | Fields/service/UI/config | Pinede license key/expiry | Tenant licensing boundary; Admin later | Medium |
| Admin provisioning | ❓ Cannot determine | scripts/settings/workflows | Some operational paths | Clinic assumptions | Future Admin boundary | Medium |
| Frontend auth guard | 🟡 Partial | App.jsx/api/index.js | Client localStorage guard | Pinede storage keys | Server authorization, XSS-resistant token strategy | Critical |
| Frontend visual system | ✅ Existing and valuable | Layout/styles/components | Sidebar/cards/tables/forms | Branding text/logo | Preserve; accessibility audit | Medium |
| Localization | 🟡 Partial | `i18n.js`, mixed French/English UI | i18n exists but mixed literals | French clinic defaults | Complete resource-based localization | Medium |
| Backend tests | ❌ Missing | solution | No test projects found | No | Add unit/integration/security tests | Critical |
| Frontend tests | 🟡 Partial | two test files | Minimal files; no test script | No | Meaningful coverage/CI | High |
| Hardware tests | ❌ Missing | repo | No automated reader/card suite | External | Lab matrix and fixtures | Critical |
| Deployment | 🟡 Partial | Docker/WiX/scripts | Dev/installer artifacts | Pinede containers/MSIs | Signed reproducible production deployment | High |

## STOP CONDITION

This report is the end of Phase 1. No Phase 2 implementation, branding conversion, redesign, refactor, migration, dependency upgrade, or bug fix was performed.
