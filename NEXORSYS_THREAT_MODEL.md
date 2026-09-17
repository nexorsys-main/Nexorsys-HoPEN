# NexorSys Identity + Kiosk Threat Model

## Trust boundaries

- Browser ↔ Identity API: untrusted network client; session cookie is HttpOnly and state-changing browser requests require the CSRF header.
- Kiosk ↔ Identity API: Production Kiosk/Fleet API-key authentication is disabled. Production requires a presented client certificate that chains to a trusted CA, is currently valid and unrevoked, and has a thumbprint bound to a workstation in the selected organization whose lifecycle state is `ACTIVE`. The canonical desktop Kiosk loads its client key from the Windows CurrentUser\My certificate store and fails closed if production configuration is absent.
- Identity API ↔ PostgreSQL: server-side tenant filters and explicit organization checks are authoritative.
- Kiosk/Agent ↔ Windows: native provider and hardware behavior require external validation on a signed Windows build.
- Identity API ↔ LDAP/AD and external healthcare applications: provider-specific infrastructure is deployment-controlled and not mocked in production.

## Main threats and controls

| Threat | Existing control | Test/status | Residual risk |
|---|---|---|---|
| Cross-tenant object access | EF organization query filters, claim-bound org, explicit resource checks | Automated tenant tests pass | Requires staging API tests against PostgreSQL |
| Cookie theft/CSRF | HttpOnly/Secure/SameSite session cookie, CSRF double-submit header | Frontend CSRF test passes | HTTPS and browser staging validation required |
| Revoked user continues session | JWT validation checks active user and active, recent server session; logout closes session | Unit path implemented | Requires live revocation integration test |
| Vault credential disclosure | Data Protection at rest; dedicated Agent-certificate mTLS route; 20-second one-use grant; serializable redemption rechecks authorization; no-store response | PostgreSQL release integration test passes success/replay/concurrency/permission-revocation cases | Windows Agent key ACL, IPC, OS user/process/session binding, and app-specific delivery remain unimplemented |
| Unauthorized application launch | DB application active flag + user permission checked before app session start | Source-side control implemented | Native launch allowlist must be tested on Windows |
| Kiosk spoofing | Production request requires CA-valid client certificate + active lifecycle state + thumbprint/org binding; dev API key limited to Development | Lifecycle and Vault denial tests pass; no real CA/certificate exercise | Customer CA/issuance/rotation and external PKI validation remain required |
| Stale/revoked workstation | Explicit lifecycle state; revoke/replace/decommission disable workstation and close bound sessions | 11 focused backend tests pass; no live PostgreSQL/API integration run | Database upgrade and customer device lifecycle validation remain required |
| PIN disclosure | BCrypt hash; temporary PIN endpoint refuses plaintext delivery | Source review | Secure out-of-band reset delivery remains external work |
| LDAP compromise | External configuration, LDAPS setting, no source secret | Configuration reviewed | Certificate/AD integration validation required |
| Native provider abuse | Removed universal emergency badge/PIN and unauthenticated localhost callbacks; native provider rejects authentication until a secure Agent contract exists | Native DLL and unsigned engineering MSI build; auth intentionally unavailable | Windows Agent service, signing, ACL/IPC review, TPM/DPAPI and real Winlogon tests required |
| Audit poisoning | Audit writes derive organization and actor from server context; no mock fallback | Backend build passes | Append-only/WORM integrity deployment control required |
| Rogue app launch | Identity application-session authorization required; Kiosk permits HTTPS URLs or existing EXEs under Program Files only | Source-side checks built; no customer app integration run | Signed app policy, per-app executable validation, customer application tests required |
| Vault crossover | Release requires active application permission, current user session, active workstation and matching live application session; one-use mTLS Agent grant is consumed atomically; browser REST never returns plaintext | PostgreSQL integration covers one-use, replay, concurrency and post-grant permission removal | Agent service/IPC, Windows user/process binding, and app delivery are not implemented; no end-to-end release claim |
| Fake provider success | NFC PIN uses BCrypt verification and active/lock checks; PSC/e-CPS stub success and provider token validation return false | Provider behavior has executable tests | Provider-specific real hardware/issuer validation required |

## Phase 3 release-hardening notes

- The sole production WPF project is `User-Administration/kiosk-nfc`; the root `Pinede.NFC.APP` folder is explicitly labeled legacy reference and its portable publishing script refuses to publish it.
- The canonical Kiosk no longer includes its local password-gated admin/settings pages or simulated badge button.
- Phase 4 adds an application-side enrollment workflow: operator approval associates a customer-issued certificate thumbprint, then certificate proof-of-possession activates the device. Revocation/replacement/decommission fail closed and close bound sessions. No certificate authority, issuance, renewal, or production revocation service is configured in this repository.
- The Credential Provider's PIN/reset/session backchannels are deliberately disabled; the current native binary cannot authenticate until the signed Agent/mTLS contract is implemented.
- Production Identity/Kiosk cannot run end-to-end from this workspace because production URLs, database, TLS/CA and enrolled certificates are deliberately absent. This is fail-closed behavior, not a successful integration test.
- Existing workstation rows migrate to `PENDING_APPROVAL`; administrators must review and re-enroll them. The migration has not been exercised against PostgreSQL.
- The Windows Agent service, IPC, Windows identity/process binding, and application delivery remain unimplemented. The Credential Provider continues to fail closed.
- Phase 4 now implements backend Agent certificate identity/proof and a dedicated mTLS one-use Vault grant/redeem route. This is not a Windows Agent or application delivery implementation, and must not be exposed operationally until the Agent exclusively controls the private key.

## Explicit external validation requirements

NFC/CPS/CPx, FIDO2/WebAuthn hardware ceremonies, real AD/LDAPS certificates and disable events, mTLS/device certificate enrollment, Windows Credential Provider/Winlogon, application launch allowlists, production HTTPS, backup/restore, penetration testing, and shared-workstation hardware behavior.
