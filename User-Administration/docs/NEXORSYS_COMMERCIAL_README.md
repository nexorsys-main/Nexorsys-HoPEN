# NexorSys Identity + Kiosk (1.5.2 Commercial Release)

Commercial engineering package for identity, access control, audit, workstation trust, kiosk authorization, and Vault secure credential management.

This package is a **frozen release candidate (1.5.2)**. Configure all database, directory, certificate, licensing, and workstation values through the deployment process and an approved secret provider. No customer environment, hardware integration, or production signing is included.

**Release SHA-256:** `C3E1E4E0988D3BD6EDA9EE0DAD8F7197FB1F4B334CE42DFEB118D933E854AEE4`

Before distribution, the release owner must:

- apply reviewed database migrations (including the Vault schema extensions);
- provision durable data-protection keys and the approved encryption certificate with restrictive ACLs (required for Vault AES-256-GCM authenticated encryption);
- provision workstation and agent certificates and validate the trust chain;
- configure the authorized directory and application allowlist;
- sign the binaries and installer with the approved code-signing certificate and timestamp service;
- perform customer-host installation, recovery, backup/restore, and security validation.

Vault credential administration (1.5.2) is fully active and protected by explicit tenant isolation, `identity` licensing entitlement, and `Administrator`/`VaultAdministrator` role requirements.
Plaintext credential release remains securely isolated to authenticated Windows Agents and is not exposed via REST interfaces.
