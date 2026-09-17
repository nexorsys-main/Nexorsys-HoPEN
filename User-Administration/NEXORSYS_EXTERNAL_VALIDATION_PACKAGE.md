# NexorSys Identity 1.5.2 — External Validation & Commercial Signing Package

## 1. Release Candidate Identification

**Product**: NexorSys Identity 1.5.2  
**Status**: FROZEN / READY FOR EXTERNAL VALIDATION  
**Primary Artifact**: `NexorSys-Identity-Kiosk-1.5.2.zip`  
**Included Components**: Identity API, Control Center UI, Windows Agent, Windows Kiosk, Database Migrations, SBOM, Documentation.

### Current Validation State Matrix
- **SOFTWARE IMPLEMENTED**: Yes (Backend, UI, NFC Polling, Tenant Isolation).
- **AUTOMATED TESTED**: Yes (CrossTenantIsolationTests, Authorization, Core logic).
- **LOCAL VALIDATION**: Yes (Development/Staging environments).
- **EXTERNAL CUSTOMER VALIDATION**: **NO** (Pending - AD/LDAPS, mTLS PKI, physical NFC/CPS).
- **INDEPENDENT SECURITY ASSESSMENT**: **NO** (Pending).
- **COMMERCIAL SIGNING**: **NO** (EV Code Signing is pending).
- **PRODUCTION DEPLOYMENT**: **NO** (Pending all above steps).

---

## 2. SHA-256 Verification Procedure

Before initiating any deployment or signing operations, the validation engineer MUST verify the integrity of the release artifact.

1. Download or locate `NexorSys-Identity-Kiosk-1.5.0-8e63cc6cbbfa.zip`.
2. Execute the following command in PowerShell:
   ```powershell
   Get-FileHash -Path .\NexorSys-Identity-Kiosk-1.5.0-8e63cc6cbbfa.zip -Algorithm SHA256
   ```
3. Ensure the output strictly matches:
   `9A481FD5AFDC2AC25BB466BAADE932DE838DC50081A45EF3EE833418C51538EB`
4. If the hash does NOT match, the artifact is compromised or corrupted. STOP IMMEDIATELY.

---

## 3. Commercial Signing Procedure

Code signing guarantees that the native Windows binaries were legitimately produced by the vendor and have not been tampered with.

1. Provision an offline or strictly controlled signing workstation.
2. Connect the HSM/Smartcard containing the enterprise EV Code Signing Certificate.
3. Extract the contents of the verified ZIP artifact.
4. Execute `signtool` against all executable assemblies:
   ```cmd
   signtool sign /tr http://timestamp.digicert.com /td sha256 /fd sha256 /a "path\to\Nexorsys.WindowsAgent.exe"
   signtool sign /tr http://timestamp.digicert.com /td sha256 /fd sha256 /a "path\to\NexorSysKiosk.exe"
   ```
   *(Repeat for all `.exe` and `.dll` binaries intended for the endpoint).*
5. Re-package the signed binaries into the final customer deployment format (e.g., MSI/InnoSetup).
6. Sign the final installer package.

---

## 4. Acceptance Criteria & Clean-Machine Validation Procedure

Validation must occur on hardware that has NEVER had NexorSys installed or cached.

### 4.1 Installation & Startup
- [ ] **Procedure**: Install the signed MSI/Agent on a clean Windows 11 endpoint. Deploy the API and PostgreSQL to a staging server.
- [ ] **Acceptance**: The backend initializes successfully. The Windows Agent registers as a service and communicates with the backend without throwing SSL trust errors.

### 4.2 AD/LDAPS Integration Test
- [ ] **Procedure**: Configure the backend LDAP sync against a staging Active Directory instance with LDAPS (port 636) enabled.
- [ ] **Acceptance**: AD Users and Groups sync correctly. Passwords are not synced, but authentication binds successfully against the AD domain controller. Invalid certs must hard-fail.

### 4.3 NFC Hardware Validation
- [ ] **Procedure**: Attach the supported PC/SC smartcard reader to the endpoint. Tap a known NFC badge.
- [ ] **Acceptance**: The Kiosk UI immediately registers the UID and prompts for the user's PIN. 

### 4.4 Windows Agent & Kiosk Test
- [ ] **Procedure**: Authenticate on the Kiosk.
- [ ] **Acceptance**: A secure Windows session is established. The previous Kiosk shell is suspended or hidden, and the user's designated applications become available. Upon logout or session timeout, the Windows Agent successfully force-terminates all user processes and restores the Kiosk lock screen.

### 4.5 Credential Provider / Winlogon Test
- [ ] **Procedure**: Install the NexorSys Credential Provider DLL into the Windows System32 registry path.
- [ ] **Acceptance**: The native Windows logon screen intercepts standard credential entry and delegates control to the NexorSys Kiosk layer. Standard domain credential bypass is disabled.

### 4.6 Application Integration Test
- [ ] **Procedure**: Launch an assigned application (e.g., an EMR client) via the Kiosk.
- [ ] **Acceptance**: The application launches successfully. Any Vault credentials assigned to the application are injected correctly into the application context.

### 4.7 Licensing, Activation & Revocation Test
- [ ] **Procedure**: Apply an expired license, or revoke a user/workstation mid-session from the Control Center.
- [ ] **Acceptance**: The expired license halts authentication entirely. The revoked user/workstation session is terminated within 30 seconds by the Windows Agent via SignalR orchestration.

### 4.8 Backup and Restore Test
- [ ] **Procedure**: Execute `pg_dump` on the staging database. Destroy the database container. Stand up a new database and execute `pg_restore`.
- [ ] **Acceptance**: The backend reconnects successfully. All tenant isolation, users, and audit logs are preserved intact.

---

## 5. Independent Security Validation Checklist

A qualified third-party security firm must conduct the following assessment before the final GO gate.

- [ ] **Penetration Testing**: Attempt to compromise the API from an unauthenticated external perspective.
- [ ] **Authorization / IDOR**: Authenticate as a standard user in Tenant A and attempt to modify resources in Tenant B using direct API object references.
- [ ] **Tenant Isolation**: Attempt to spoof the `X-Organization-Id` header to access a different tenant's data. (Expected result: Failure; tenant is bound by JWT `org_id` claim).
- [ ] **Authentication / Session**: Attempt to replay a JWT token after remote revocation.
- [ ] **Windows Agent / IPC**: Attempt to bypass the Windows Agent service by killing the process or elevating privileges via DLL hijacking.
- [ ] **Vault Security**: Attempt to extract Vault credentials from the backend database or network transit. (Expected result: Credentials must remain inaccessible without explicit hardware/license guard clearance).
- [ ] **Infrastructure Review**: Validate TLS configurations (mTLS where required), database encryption at rest, and firewall perimeter rules.

---

## 6. Final GO / NO-GO Release Gate

**NO-GO Conditions (Halt Release):**
- Any failure in the SHA-256 verification of the frozen artifact.
- Any critical or high-severity vulnerabilities identified during the independent security assessment.
- Any failure in Tenant Isolation, Credential Provider security, or Agent session termination.
- Failure to secure an EV Code Signing operation.

**GO Conditions (Proceed to Commercial Delivery):**
- All external validation test procedures produce expected ACCEPTANCE results.
- Third-party security assessment yields zero critical/high findings, and all medium findings have documented risk-acceptance.
- The compiled installer package is successfully EV-signed and timestamped.
- **Action**: Mark NexorSys Identity 1.5.0 as **FULLY COMMERCIALLY RELEASED**.
