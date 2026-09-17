# NexorSys Phase 4 P0.1 — Windows Agent implementation status

## Result

The repository now contains the first Windows-only Agent service host and authenticated named-pipe foundation. It is **not a production credential-release path**. The service intentionally returns `RELEASE_NOT_CONFIGURED` for every syntactically valid release request. Do not deploy it as a credential-delivery service or change that response until the blocking trust bindings below are implemented and tested.

## Implemented

- `backend/src/Nexorsys.WindowsAgent` is a .NET 8 Windows Service host using SCM lifecycle and graceful host cancellation. It has no HTTP listener or browser/API-key fallback.
- The local pipe is `Nexorsys.Identity.Agent.v1`, byte-mode, bounded to 16 KiB, and ACL-restricted to LocalSystem and authenticated users, with anonymous access denied. Only `vault.release` protocol version 1 is recognized.
- The service obtains the pipe impersonation SID, kernel-reported client PID/session, process token SID, and executable path from Windows. It denies when the pipe-token SID and process-token SID differ. It does not accept caller-supplied SID/PID/session fields.
- Messages have strict length/version/operation/ID/session/application/time validation, 15-second maximum lifetime, 5-second clock skew, and a 10-second pipe-read deadline.
- The in-memory replay cache binds SID + operation + request ID, expires entries, and caps storage at 20,000 entries. At capacity it rejects new requests until existing entries expire (it never evicts live replay records).
- Core policy primitives require both an application install-root match and publisher-thumbprint match. These observed values are not yet wired to trusted Authenticode/process-image verification or server registration.
- Automated tests cover protocol parsing, oversize/malformed/unsupported/expired requests, replay bounds/principal binding, and application root/publisher mismatch.

## Not implemented / release blockers

- No production service installer or verified dedicated least-privilege service identity deployment is included. SCM hosting support exists, but this code has not been installed or exercised as an actual service on a Windows host.
- Agent certificate enrollment/store lookup, enrolled-workstation matching on the Agent, private-key access validation, and private-key ACL provisioning are not wired. The existing API mTLS endpoint remains untouched and is not called by this service.
- Windows user SID/session does not yet map authoritatively to the existing Identity `UserSession`; the pipe request's session/application IDs are merely syntactic inputs, not trusted claims. No server-side enrollment/binding record exists to verify them against the derived caller.
- Application path/publisher policy is only a core predicate. The Agent does not yet inspect/verify the requester executable signature, installation registration, or backend application/session authorization.
- There is no approved customer application credential-delivery adapter. No credential is returned over IPC, no process is launched, and clipboard/file delivery is absent.
- The existing API currently returns the redeemed secret to its mTLS caller. Until the Agent owns and exclusively protects its certificate private key, do not expose/use that route operationally.
- No Windows-hosted pipe impersonation/ACL/service/certificate tests were run. The unit tests do not establish production Windows security behavior.

## Verification

- Windows-target Agent project build: passed, 0 warnings / 0 errors.
- Identity solution build: passed, 0 warnings / 0 errors (incremental build).
- Security tests: 19 passed, 1 PostgreSQL-dependent test skipped because the integration environment was not configured in this run.
- Windows service installation, named-pipe client integration, process signature verification, and service certificate-key ACL: not tested.

## Deployment security contract

Run only as a dedicated non-administrator virtual service account with a restricted service SID. Provision an already customer-enrolled Agent certificate into the Local Computer personal store; grant the service identity private-key read access only, and remove inherited broad access where supported. Provision its expected thumbprint through an authenticated enrollment mechanism, not source, JSON, environment variables, or caller input. Verify the ACL and key use under the actual service token before enabling mTLS. Missing or unverified configuration must continue to deny. Never run as LocalSystem as a convenience default.

The next required integration is an authenticated backend binding for `(Organization, Workstation, Agent certificate, Windows SID, Windows session, Identity UserSession)`, plus approved signed-app registration and a customer-specific delivery adapter. Only then can the existing grant/redeem service be called from the Agent.
