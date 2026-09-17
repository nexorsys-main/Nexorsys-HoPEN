# NexorSys Identity & Kiosk - Product Overview

## 1. What is NexorSys Identity?
NexorSys Identity is a comprehensive centralized authentication, authorization, and fleet management platform tailored for secure environments. It serves as the primary Identity and Access Management (IAM) authority, orchestrating cross-tenant user lifecycles, role-based access control, and organization-bound workstation trust. 

## 2. What is NexorSys Kiosk?
NexorSys Kiosk is the native Windows execution layer designed for shared workstations in high-turnover enterprise or clinical environments. It allows users to authenticate seamlessly via an NFC badge and PIN, establishing a securely orchestrated local Windows session bounded by the identity platform's strict authorization rules.

## 3. Target Organizations
NexorSys is engineered for enterprises, healthcare facilities, and highly regulated environments where secure, rapid session switching on shared terminals is mandatory.

## 4. Deployment Model
The solution is deployed entirely on-premises or within a customer's private cloud infrastructure. It comprises a centralized ASP.NET Core API backend, a PostgreSQL database for persistent state and audit trails, a React-based Control Center SPA for administration, and native Windows agents/kiosk executables distributed to endpoint workstations.

## 5. Security Model
NexorSys employs a strict, fail-closed security architecture. All critical authorization decisions are resolved server-side. Multi-tenant isolation is cryptographically enforced via JWT claims and explicit organization contexts rather than trusting client-provided headers. Vault credential release is heavily gated and audited. 

## 6. Authentication
- **Local Identity**: Secure BCrypt-hashed local profiles.
- **Active Directory / LDAPS**: Enterprise directory integration with mapped identities.
- **NFC Badge**: Hardware token tap-and-go orchestration.

## 7. Workstation Security
Endpoints are cryptographically bound to the NexorSys Identity infrastructure. Workstations that are unrecognized, unenrolled, or revoked are proactively denied authentication capability. The Windows Agent maintains a persistent heartbeat and enforces remote session termination.

## 8. Application Access
NexorSys facilitates authorized launching of predefined applications (e.g., Electronic Medical Records or enterprise software) from the Kiosk. Launch requests are validated against user roles, module entitlements, and specific executable whitelists to prevent malicious process execution.

## 9. Directory Integration
Native LDAPS integration allows organizations to synchronize users and map groups securely to internal NexorSys roles without duplicating identity administration.

## 10. Audit
A centralized, immutable audit log tracks all significant security operations, including authentication attempts, policy modifications, and profile changes. No credential material or sensitive Vault payloads are ever written to the audit log.

## 11. Licensing
The platform incorporates strict cryptographic license enforcement. Access requires a valid, signed enterprise entitlement verifying the organization, product version, and module-specific quotas. The system fails closed upon license expiration or validation failure.

## 12. Deployment Prerequisites
- Enterprise virtualization environment for backend hosting (Docker/Kubernetes or IIS).
- PostgreSQL 16+ relational database.
- Internal PKI for LDAPS and workstation trust binding.
- Valid EV Code Signing Certificate for customer distribution of native Windows binaries.

## 13. Current Limitations
- FIDO2 / WebAuthn and CPS/e-CPS authentication modules are currently **NOT IMPLEMENTED**.
- Offline workstation authentication is not supported; the Kiosk requires continuous line-of-sight to the Identity API.

## 14. External Validation Requirements
The following components are fully implemented in software but strictly require deployment into a physical customer environment for final validation:
- **LDAPS**: Customer Active Directory certificate trust binding.
- **NFC**: Physical smartcard reader testing.
- **Windows Credential Provider**: Winlogon integration and EV code signing.
- **Disaster Recovery**: Multi-node cluster restoration.
