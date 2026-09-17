# Phase 6 Product Capability Gaps

This list distinguishes implemented route surfaces from product features that are absent or only partially represented. Missing features are not treated as secure merely because their entities, UI labels, or documentation exist; this phase does not add placeholder endpoints to make an authorization inventory appear complete.

## No complete backend route family found in the current API assembly

- Organization site-selection lifecycle / delegated site administration.
- Group membership administration and group-to-application authorization.
- Role and permission administration beyond current user/application grant checks.
- MFA enrollment/challenge/recovery lifecycle and FIDO2/WebAuthn credential lifecycle.
- Emergency/break-glass access with approval, expiry, and post-event review.
- Complete application assignment management and application catalog administration.
- Customer directory configuration / full AD/LDAPS provisioning lifecycle.

## Partial surfaces that remain constrained or disabled

- Instance-global Settings APIs are restricted to deployments with at most one organization because their JSON configuration and DB/LDAP/SMTP probes are process-wide, not tenant-scoped. In a multi-organization deployment they fail closed with 409; tenant license activation remains organization-bound. A future platform-operator/configuration-management boundary is a product capability gap; the current API does not pretend global settings are tenant-specific.
- Department policy CRUD persists tenant-scoped settings, and the standalone risk evaluator now scopes lookups to the user's organization and rejects a cross-tenant user/workstation pair. However, repository-wide call-site search finds no authentication-flow caller for that evaluator; version enforcement was an unconditional-true stub and has been removed. Thus risk levels and allowed hours are not enforced by authentication. The policy UI warns administrators explicitly; this remains an integration/product gap until approved assurance semantics and a tested auth-flow integration exist.
- Windows authentication routes: enrollment/revoke/reenroll operations exist; retired legacy validation, workstation-list, provider-health, and session-token actions have been removed because they lacked trusted Agent proof. Session creation remains an explicit 501 until cryptographic Windows logon proof is available.
- Federation: provider listing exists, but PSI/e-CPS callbacks intentionally return not implemented; no federated token is synthesized.
- NFC/ANS: management surfaces exist, but CUID writing, badge pairing and declaration are disabled until real adapters are approved and tested.
- Device certification metadata is an administrator-entered attestation/reference only. The API does not query or validate ANS/PSI; the interface therefore describes it as an administrative attestation, requires a bounded reference, and must not be treated as proof of external certification.
- Vault: Administration, tenant isolation, and AES-256-GCM encryption are fully implemented (1.5.2). However, plaintext credential release intentionally remains omitted from the REST API to ensure security; it relies on the authenticated Windows Agent/Credential Provider pipeline for final payload decryption.
- Kiosk application launch: one-time app-session authorization now flows Kiosk→named-pipe Agent→mTLS Identity→Kiosk; a controller-level mTLS action filter guards every Agent action, the Agent verifies signed publisher/hash/path, and the interactive Kiosk process launches with no arguments. It fails closed until machine registry trust values, certificates and production binaries are provisioned; deployed Windows service/Winlogon/customer-app behavior remains unvalidated.
- License revocation distribution: a configurable mTLS client, signed feed contract and atomic monotonic cache are implemented; no NexorSys Admin feed server or customer endpoint is part of this repository.

Use the generated `api-route-inventory.json` alongside the source authorization matrix. It enumerates controller route/action attributes, framework `[AllowAnonymous]` metadata, and additional declared authentication filters (including the Agent mTLS filter). The API now has a global authenticated-user fallback policy; only explicit `[AllowAnonymous]` routes bypass that policy, and Agent anonymous-to-JWT routes still require the Agent certificate action filter. It does not itself prove resource-level tenant authorization; endpoint-specific A→B tests remain required.
