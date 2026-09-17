# NexorSys Installer Status and Release Contract

## Current status

There is no commercial Identity + Kiosk installer in the source tree. `User-Administration/kiosk-windows-auth/build-phase3/NexorSysIdentityCredentialProvider.msi` is an unsigned engineering-only Credential Provider package. It is not a customer installer and must not be installed or distributed. The canonical WPF Kiosk is `User-Administration/kiosk-nfc`; the root `Pinede.NFC.APP` and historical installer outputs are legacy/reference material.

## Required commercial package allowlist

A future signed product installer may include only explicitly versioned, built production components and approved runtime prerequisites. It must not include source, tests, development tools, debug utilities, temporary files, `.pdb`/`.wixpdb`, old MSI packages, historical SQL, customer exports, demo credentials/secrets, root legacy Kiosk, or arbitrary files discovered by recursive workspace packaging.

## Required lifecycle before release

- Validate Windows edition, architecture, service prerequisites, and supported OS version before changing the machine.
- Install an authenticated Agent as a dedicated least-privilege service; create restrictive service and named-pipe ACLs; do not configure unauthenticated local HTTP.
- Install only a signed Credential Provider and canonical Kiosk. Use stable product/service/upgrade codes and detect installed versions before upgrade.
- Preserve customer configuration outside the binary payload; never overwrite customer certificates/private keys or Data Protection keys. Configuration must be validated before service activation.
- Upgrade transaction must stop services safely, preserve policy/config, replace signed binaries, validate health, and roll back on failure.
- Uninstall must stop/remove only product-owned services, files, provider registration, and firewall/config entries; it must not delete customer data, shared certificates, unrelated registry keys, or identity records. Offer explicit separately authorized data-retention/deletion steps.
- Logs must omit secrets and tokens and be bounded/rotated. Installer logs and rollback evidence must be retained without credential material.
- Authenticode signing requires an external organization-controlled certificate/HSM or signing service and audited key custody. Unsigned output is engineering-only.

## Build and package verification gate

Use repository-relative staging paths and a clean staging directory. Build from pinned source and dependencies, then generate a package manifest containing path, size, SHA-256, version, signer, and component classification. Compare every payload item against the allowlist; fail if any historical backup, customer-data file, source file, test utility, unsigned executable, unknown file, or legacy Kiosk appears. Inspect MSI tables and installed-file inventory, then test fresh install, upgrade, rollback, repair, uninstall, and reinstall on clean disposable Windows VMs. Verify registry/service/ACL cleanup and preserve customer state.

None of these commercial installer gates is currently implemented or validated. The existing engineering MSI must remain outside release staging.
