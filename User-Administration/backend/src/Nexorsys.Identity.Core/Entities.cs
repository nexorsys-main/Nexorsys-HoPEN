namespace Nexorsys.Identity.Core
{
	using System;
	using System.Collections.Generic;

	public class User
	{
		public Guid Id { get; set; }
		public Guid OrganizationId { get; set; }
		public string? AdGuid { get; set; }
		public string SamAccountName { get; set; } = string.Empty;
		public string DistinguishedName { get; set; } = string.Empty;
		public string? DisplayName { get; set; }
		public string? FirstName { get; set; }
		public string? LastName { get; set; }
		public string? Email { get; set; }
		public string? PhoneNumber { get; set; }
		public string? PasswordHash { get; set; }
		public string? Department { get; set; }
		public string? Title { get; set; }
		public string? ManagerAdGuid { get; set; }
		public string? EmployeeId { get; set; }
		public string? BadgeUid { get; set; }
		public string? CpsId { get; set; }
		public string? FidoId { get; set; }
		public string? BadgeType { get; set; } = "NFC_Badge";
		[Newtonsoft.Json.JsonProperty("isActive")]
		public bool IsActive { get; set; }

		public bool IsLocalProfile { get; set; }
		public string? RppsNumber { get; set; } // Added for ANS mapping
		public string Role { get; set; } = "VIEWER"; // SUPERADMIN, ADMIN_DSI, ADMIN_RH, DIRECTOR, VIEWER

		[Newtonsoft.Json.JsonProperty("mustChangePin")]
		public bool MustChangePin { get; set; }
		public int FailedPinAttempts { get; set; }
		public DateTime? LockedUntil { get; set; }
		public DateTime? LastLoginAt { get; set; }
		public DateTime CreatedAt { get; set; }
		public DateTime UpdatedAt { get; set; }

		// Enterprise: Windows Session Auth & Enrollment Lifecycle
		public DateTime? EnrollmentExpirationDate { get; set; }
		public DateTime? LastCredentialRefresh { get; set; }
		public string? EnrollmentWorkstation { get; set; }
		public bool RequiresReEnrollment { get; set; }
		public bool WindowsAuthEnabled { get; set; }
		public string? WindowsAuthMode { get; set; }
		public string? EnrollmentStatus { get; set; }
		public bool CredentialProviderEnabled { get; set; }
		public DateTime? LastWindowsLogin { get; set; }
		public User()
		{
			UserPins = new List<UserPin>();
			UserPermissions = new List<UserPermission>();
			Workflows = new List<Workflow>();
			KioskSessions = new List<KioskSession>();
			Devices = new List<UserDevice>();
			UserWorkstations = new List<UserWorkstation>();
			UserSessions = new List<UserSession>();
			AuthenticationEvents = new List<AuthenticationEvent>();
		}

		public virtual ICollection<UserPin>? UserPins { get; set; }
		public virtual ICollection<UserPermission>? UserPermissions { get; set; }
		public virtual ICollection<Workflow>? Workflows { get; set; }
		public virtual ICollection<KioskSession>? KioskSessions { get; set; }
		public virtual ICollection<UserDevice>? Devices { get; set; }
		public virtual ICollection<UserWorkstation>? UserWorkstations { get; set; }
		public virtual ICollection<UserSession>? UserSessions { get; set; }
		public virtual ICollection<AuthenticationEvent>? AuthenticationEvents { get; set; }
	}

	public class UserPin
	{
		public Guid Id { get; set; }
		public Guid OrganizationId { get; set; }
		public Guid UserId { get; set; }
		public string BadgeUid { get; set; } = string.Empty;
		public string PinHash { get; set; } = string.Empty;
		public bool IsActive { get; set; }
		public DateTime CreatedAt { get; set; }
		public DateTime UpdatedAt { get; set; }
		public Guid? CreatedBy { get; set; }

		public virtual User? User { get; set; }
		public virtual User? CreatedByUser { get; set; }
	}

	public class Application
	{
		public Guid Id { get; set; }
		public Guid OrganizationId { get; set; }
		public string Name { get; set; } = string.Empty;
		public string? Description { get; set; }
		public string ClientId { get; set; } = string.Empty;
		public string? ClientSecret { get; set; }
		public string[] RedirectUris { get; set; } = Array.Empty<string>();
		public bool IsActive { get; set; }
		public DateTime CreatedAt { get; set; }
		/// <summary>Administrator-approved executable root and publisher identity for the Windows Agent; empty means not registered.</summary>
		public string? ApprovedInstallRoot { get; set; }
		public string? ApprovedExecutablePath { get; set; }
		public string? ExpectedPublisherThumbprint { get; set; }
		public string? ExpectedExecutableSha256 { get; set; }
		/// <summary>Only "none" is currently supported; credential delivery is not enabled.</summary>
		public string CredentialDeliveryMechanism { get; set; } = "none";
		public DateTime? AgentRegistrationUpdatedAt { get; set; }
		public Guid? AgentRegistrationUpdatedBy { get; set; }

		public virtual ICollection<UserPermission>? UserPermissions { get; set; }
	}

	public class UserPermission
	{
		public Guid Id { get; set; }
		public Guid OrganizationId { get; set; }
		public Guid UserId { get; set; }
		public Guid ApplicationId { get; set; }
		public string PermissionLevel { get; set; } = "read";
		public DateTime GrantedAt { get; set; }
		public Guid? GrantedBy { get; set; }
		public DateTime? ExpiresAt { get; set; }

		public virtual User? User { get; set; }
		public virtual Application? Application { get; set; }
		public virtual User? GrantedByUser { get; set; }
	}

	public class AuditLog
	{
		public Guid Id { get; set; }
		public Guid OrganizationId { get; set; }
		public Guid? UserId { get; set; }
		public string Action { get; set; } = string.Empty;
		public string? ResourceType { get; set; }
		public Guid? ResourceId { get; set; }
		public string? OldValues { get; set; }
		public string? NewValues { get; set; }
		public string? IpAddress { get; set; }
		public string? UserAgent { get; set; }
		public string? CorrelationId { get; set; }
		public DateTime CreatedAt { get; set; }

		public virtual User? User { get; set; }
	}

	public class Workflow
	{
		public Guid Id { get; set; }
		public Guid OrganizationId { get; set; }
		public string Type { get; set; } = string.Empty;
		public Guid UserId { get; set; }
		public string Status { get; set; } = "pending";
		public Guid? AssignedTo { get; set; }
		public Guid? AssignedBy { get; set; }
		public string? Comments { get; set; }
		public string? FormData { get; set; }
		public DateTime CreatedAt { get; set; }
		public DateTime UpdatedAt { get; set; }

		public virtual User? User { get; set; }
		public virtual User? AssignedToUser { get; set; }
		public virtual User? AssignedByUser { get; set; }
	}

	public class KioskSession
	{
		public Guid Id { get; set; }
		public Guid OrganizationId { get; set; }
		public Guid UserId { get; set; }
		public Guid? WorkstationId { get; set; }
		public string SessionToken { get; set; } = string.Empty;
		public string BadgeUid { get; set; } = string.Empty;
		public string NfcUid { get; set; } = string.Empty;
		public DateTime ExpiresAt { get; set; }
		public DateTime CreatedAt { get; set; }

		public virtual User? User { get; set; }
	}

	public class AnsDelegationLog
	{
		public Guid Id { get; set; }
		public Guid OrganizationId { get; set; }
		public Guid UserId { get; set; }
		public string RppsNumber { get; set; } = string.Empty;
		public string NfcBadgeUid { get; set; } = string.Empty;
		public string? CredentialDigestSha256 { get; set; }
		public string VerificationMethod { get; set; } = string.Empty;
		public DateTime PairedAt { get; set; }
		public Guid? AdminUserId { get; set; }
		public bool IsDeclaredToGovernment { get; set; }

		public virtual User? User { get; set; }
		public virtual User? AdminUser { get; set; }
	}

	// ═══════════════════════════════════════════════════════════════════
	// FEDERATION LAYER — PSI / e-CPS / OpenID Connect Extension
	// Added for healthcare identity federation compatibility.
	// All existing entities above remain UNCHANGED.
	// ═══════════════════════════════════════════════════════════════════

	public class IdentityProvider
	{
		public Guid Id { get; set; }
		public Guid OrganizationId { get; set; }
		public string ProviderName { get; set; } = string.Empty;
		public string ProviderType { get; set; } = string.Empty; // NFC, ActiveDirectory, PSI, eCPS, OAuth, OpenIDConnect
		public bool IsEnabled { get; set; }
		public int Priority { get; set; } // Lower = higher priority
		public string? ConfigurationJson { get; set; } // JSONB — provider-specific config
		public string HealthStatus { get; set; } = "unknown"; // healthy, degraded, offline, unknown
		public DateTime? LastHealthCheck { get; set; }
		public DateTime CreatedAt { get; set; }
		public DateTime UpdatedAt { get; set; }
	}

	public class FederationToken
	{
		public Guid Id { get; set; }
		public Guid OrganizationId { get; set; }
		public Guid UserId { get; set; }
		public string ProviderType { get; set; } = string.Empty;
		public string TokenReference { get; set; } = string.Empty;
		public string? ExternalSubjectId { get; set; } // External identity (e.g., RPPS from PSI)
		public string? AssuranceLevel { get; set; }
		public DateTime IssuedAt { get; set; }
		public DateTime ExpiresAt { get; set; }
		public bool IsRevoked { get; set; }

		public virtual User? User { get; set; }
	}

	public class MigrationPhase
	{
		public Guid Id { get; set; }
		public Guid OrganizationId { get; set; }
		public string PhaseName { get; set; } = string.Empty; // Phase1_Operational, Phase2_HybridPilot, Phase3_Federated, Phase4_FullFederation
		public string? Description { get; set; }
		public string? ActiveProvidersJson { get; set; } // JSONB — list of active provider types
		public bool FederationEnabled { get; set; }
		public bool IsCurrentPhase { get; set; }
		public DateTime? ActivatedAt { get; set; }
		public DateTime CreatedAt { get; set; }
	}

	public class AuthenticationPolicy
	{
		public Guid Id { get; set; }
		public Guid OrganizationId { get; set; }
		public string PolicyName { get; set; } = string.Empty;
		public string AssuranceLevel { get; set; } = "Standard"; // Standard, Intermediate, High, Enhanced
		public Guid? ProviderId { get; set; }
		public string? TargetGroup { get; set; } // Department or role
		public string? TargetApplication { get; set; } // Application name (EMED, BlueKango, etc.)
		public bool IsActive { get; set; }
		public DateTime CreatedAt { get; set; }
		public DateTime UpdatedAt { get; set; }

		public virtual IdentityProvider? Provider { get; set; }
	}

	/// <summary>
	/// MIE (Moyen d'Identification Électronique) — PSI Device Registry.
	/// Tracks all identity devices attached to a user: NFC Badge, CPS,
	/// eCPS, FIDO2/WebAuthn keys, Carte PS, and PSI tokens.
	/// The existing User.BadgeUid field remains for backward compatibility
	/// with the NFC Kiosk client.
	/// </summary>
	public class UserDevice
	{
		public Guid Id { get; set; }
		public Guid OrganizationId { get; set; }
		public Guid UserId { get; set; }
		public string DeviceType { get; set; } = string.Empty; // NFC_Badge, CPS, eCPS, FIDO2, CartePS, PSI
		public string DeviceName { get; set; } = string.Empty; // Human-readable label
		public string? DeviceIdentifier { get; set; } // UID, serial number, or token reference
		public string? DeviceSerial { get; set; } // Physical serial number if applicable
		public string Status { get; set; } = "active"; // active, suspended, revoked, expired, pending_certification
		public string AssuranceLevel { get; set; } = "Standard"; // Standard, Intermediate, High, Enhanced
		public bool IsPrimary { get; set; } // Primary device for this type
		public bool IsCertified { get; set; } // PSI certification status
		public string? CertificationReference { get; set; } // ANS/PSI certification number
		public DateTime? ExpiresAt { get; set; } // Expiration for CPS cards / tokens
		public DateTime? LastUsedAt { get; set; }
		public DateTime CreatedAt { get; set; }
		public DateTime UpdatedAt { get; set; }
		public Guid? RegisteredBy { get; set; } // Admin who registered the device

		public virtual User? User { get; set; }
		public virtual User? RegisteredByUser { get; set; }
	}

	// ═══════════════════════════════════════════════════════════════════
	// ENTERPRISE MODULES — Windows Session Auth, Workstations, Licensing
	// ═══════════════════════════════════════════════════════════════════

	public class Workstation
	{
		public Guid Id { get; set; }
		public Guid OrganizationId { get; set; }
		public string Hostname { get; set; } = string.Empty;
		public string? Department { get; set; }
		public string? IpAddress { get; set; }
		public string? Location { get; set; }
		public bool IsActive { get; set; }
		public bool WindowsAuthEnabled { get; set; }
		public DateTime? LastSeenAt { get; set; }
		public DateTime CreatedAt { get; set; }
		public string? DeviceCertificateThumbprint { get; set; }
		public string? AgentCertificateThumbprint { get; set; }
		public DateTime? AgentCertificateValidatedAt { get; set; }
		/// <summary>ENROLLED, PENDING_APPROVAL, ACTIVE, REVOKED, REPLACED, DECOMMISSIONED.</summary>
		public string EnrollmentState { get; set; } = WorkstationLifecycle.PendingApproval;
		public DateTime? EnrolledAt { get; set; }
		public DateTime? ApprovedAt { get; set; }
		public DateTime? LastValidatedAt { get; set; }
		public DateTime? RevokedAt { get; set; }
		public DateTime? DecommissionedAt { get; set; }
		public Guid? ReplacedByWorkstationId { get; set; }
		public string? MachineSid { get; set; }
		public string AuthenticationMode { get; set; } = "Hybrid"; // AD, Local, Hybrid

		public virtual ICollection<UserWorkstation>? UserWorkstations { get; set; }
		public virtual CredentialProviderFleet? CredentialProviderFleet { get; set; }
	}

	/// <summary>Fail-closed workstation enrollment state machine shared by API and tests.</summary>
	public static class WorkstationLifecycle
	{
		public const string Enrolled = "ENROLLED";
		public const string PendingApproval = "PENDING_APPROVAL";
		public const string Active = "ACTIVE";
		public const string Revoked = "REVOKED";
		public const string Replaced = "REPLACED";
		public const string Decommissioned = "DECOMMISSIONED";

		public static bool CanAuthenticate(Workstation workstation, string? presentedThumbprint)
		{
			if (workstation is null || !workstation.IsActive || workstation.EnrollmentState != Active ||
				string.IsNullOrWhiteSpace(workstation.DeviceCertificateThumbprint) || string.IsNullOrWhiteSpace(presentedThumbprint))
				return false;

			return NormalizeThumbprint(workstation.DeviceCertificateThumbprint) == NormalizeThumbprint(presentedThumbprint);
		}

		public static bool CanAuthenticateAgent(Workstation? workstation, string? presentedThumbprint, DateTime nowUtc)
		{
			if (workstation is null || !workstation.IsActive || workstation.EnrollmentState != Active ||
				string.IsNullOrWhiteSpace(workstation.AgentCertificateThumbprint) || string.IsNullOrWhiteSpace(presentedThumbprint) ||
				!workstation.AgentCertificateValidatedAt.HasValue || workstation.AgentCertificateValidatedAt.Value > nowUtc)
				return false;
			return NormalizeThumbprint(workstation.AgentCertificateThumbprint) == NormalizeThumbprint(presentedThumbprint);
		}

		public static string NormalizeThumbprint(string value) => value.Replace(" ", "", StringComparison.Ordinal).ToUpperInvariant();
	}

	public class UserWorkstation
	{
		public Guid OrganizationId { get; set; }
		public Guid UserId { get; set; }
		public Guid WorkstationId { get; set; }

		public virtual User? User { get; set; }
		public virtual Workstation? Workstation { get; set; }
	}

	public class CredentialProviderFleet
	{
		public Guid Id { get; set; }
		public Guid OrganizationId { get; set; }
		public Guid WorkstationId { get; set; }
		public string? InstalledVersion { get; set; }
		public DateTime? LastCheckInAt { get; set; }
		public string ProviderStatus { get; set; } = "Offline";
		public string? WindowsVersion { get; set; }
		public int EnrollmentCount { get; set; }

		public virtual Workstation? Workstation { get; set; }
	}

	public class UserSession
	{
		public Guid Id { get; set; }
		public Guid OrganizationId { get; set; }
		public Guid UserId { get; set; }
		public Guid? WorkstationId { get; set; }
		public string? Department { get; set; }
		public DateTime SessionStartedAt { get; set; }
		public DateTime? SessionEndedAt { get; set; }
		public int? DurationMinutes { get; set; }

		// New properties for unified auth
		public string? SessionToken { get; set; }
		public string? AuthenticationMethod { get; set; }
		public DateTime? LastActivityAt { get; set; }
		public string? Status { get; set; } // Active, Closed, Expired

		public virtual User? User { get; set; }
		public virtual Workstation? Workstation { get; set; }
		public virtual ICollection<SessionEvent>? SessionEvents { get; set; }
	}

	/// <summary>Admin-managed binding from a stable Windows SID to an existing NexorSys user on one workstation.</summary>
	public sealed class WindowsUserIdentityBinding
	{
		public Guid Id { get; set; }
		public Guid OrganizationId { get; set; }
		public Guid WorkstationId { get; set; }
		public Guid UserId { get; set; }
		public string WindowsSid { get; set; } = string.Empty;
		public bool IsActive { get; set; } = true;
		public DateTime CreatedAt { get; set; }
		public Guid CreatedBy { get; set; }
		public DateTime? RevokedAt { get; set; }
		public Guid? RevokedBy { get; set; }
	}

	/// <summary>Short-lived Agent-authenticated observation of one Windows logon and existing Identity UserSession.</summary>
	public sealed class AgentWindowsSessionBinding
	{
		public Guid Id { get; set; }
		public Guid OrganizationId { get; set; }
		public Guid WorkstationId { get; set; }
		public Guid UserId { get; set; }
		public Guid UserSessionId { get; set; }
		public Guid WindowsUserIdentityBindingId { get; set; }
		public string WindowsSid { get; set; } = string.Empty;
		public int WindowsSessionId { get; set; }
		public string AgentCertificateThumbprint { get; set; } = string.Empty;
		public DateTime CreatedAt { get; set; }
		public DateTime LastValidatedAt { get; set; }
		public DateTime ExpiresAt { get; set; }
		public DateTime? RevokedAt { get; set; }
	}

	public class WorkstationSession
	{
		public Guid Id { get; set; }
		public Guid OrganizationId { get; set; }
		public Guid WorkstationId { get; set; }
		public Guid? CurrentUserId { get; set; }
		public Guid? SessionId { get; set; }
		public DateTime SessionStartedAt { get; set; }
		public DateTime? SessionEndedAt { get; set; }
		public string? LastBadgeSeen { get; set; }

		public virtual Workstation? Workstation { get; set; }
		public virtual User? CurrentUser { get; set; }
		public virtual UserSession? Session { get; set; }
	}

	public class SessionEvent
	{
		public Guid Id { get; set; }
		public Guid OrganizationId { get; set; }
		public Guid SessionId { get; set; }
		public string EventType { get; set; } = string.Empty;
		public string? EventDescription { get; set; }
		public DateTime CreatedAt { get; set; }

		public virtual UserSession? Session { get; set; }
	}

	public class PinResetRequest
	{
		public Guid Id { get; set; }
		public Guid OrganizationId { get; set; }
		public Guid UserId { get; set; }
		public string BadgeUid { get; set; } = string.Empty;
		public Guid? WorkstationId { get; set; }
		public DateTime RequestedAt { get; set; }
		public DateTime? ApprovedAt { get; set; }
		public Guid? ApprovedBy { get; set; }
		public string Status { get; set; } = "Pending"; // Pending, Approved, Rejected, Completed
		public string? Reason { get; set; }

		public virtual User? User { get; set; }
		public virtual User? ApprovedByUser { get; set; }
		public virtual Workstation? Workstation { get; set; }
	}

	public class AuthenticationEvent
	{
		public Guid Id { get; set; }
		public Guid OrganizationId { get; set; }
		public DateTime Timestamp { get; set; }
		public Guid? UserId { get; set; }
		public string ProviderType { get; set; } = string.Empty;
		public Guid? WorkstationId { get; set; }
		public string? Department { get; set; }
		public string EventType { get; set; } = string.Empty;
		public string Result { get; set; } = string.Empty;
		public string? RiskLevel { get; set; }
		public string? Reason { get; set; }
		public string? IpAddress { get; set; }

		public virtual User? User { get; set; }
		public virtual Workstation? Workstation { get; set; }
	}

	public class OrganizationLicense
	{
		public Guid Id { get; set; }
		public Guid OrganizationId { get; set; }
		public string? LicenseId { get; set; }
		public string? SignedLicenseToken { get; set; }
		public long HighestRevocationSequence { get; set; }
		public int LicensedUsers { get; set; }
		public int LicensedWorkstations { get; set; }
		public string? LicensedModules { get; set; }
		public DateTime NotBefore { get; set; }
		public DateTime ExpiryDate { get; set; }
		public bool IsTrial { get; set; }
		public string? SupportLevel { get; set; }
	}

	public class FeatureFlag
	{
		public string Id { get; set; } = string.Empty; // e.g. EnableWindowsAuth
		public Guid OrganizationId { get; set; }
		public bool IsEnabled { get; set; }
		public string? Description { get; set; }
		public DateTime UpdatedAt { get; set; }
		public DateTime? EnabledFrom { get; set; }
		public Guid? EnabledByUserId { get; set; }
		public string? Reason { get; set; }
	}

	public class DepartmentPolicy
	{
		public Guid Id { get; set; }
		public Guid OrganizationId { get; set; }
		public string Department { get; set; } = string.Empty;
		public string RequiredRiskLevel { get; set; } = string.Empty;
		public TimeSpan AllowedAccessStart { get; set; }
		public TimeSpan AllowedAccessEnd { get; set; }
		public string? AllowedAuthenticationMethods { get; set; }
		public string? RequiredAssuranceLevel { get; set; }
		public string? AuthorizedWorkstations { get; set; }
		public int SessionTimeoutMinutes { get; set; }
	}

	public class ApplicationSession
	{
		public Guid Id { get; set; }
		public Guid OrganizationId { get; set; }
		public Guid UserId { get; set; }
		public Guid? UserSessionId { get; set; }
		public Guid? ApplicationId { get; set; }
		public string ApplicationName { get; set; } = string.Empty;
		public DateTime StartTime { get; set; }
		public DateTime? EndTime { get; set; }
		public DateTime? LaunchAuthorizedAt { get; set; }
		public Guid? WorkstationId { get; set; }
		public string? Department { get; set; }

		public virtual User? User { get; set; }
		public virtual UserSession? UserSession { get; set; }
		public virtual Application? Application { get; set; }
		public virtual Workstation? Workstation { get; set; }
	}

	/// <summary>One-time secret-free grant; only a hash of the opaque redemption token is persisted.</summary>
	public sealed class VaultReleaseGrant
	{
		public Guid Id { get; set; }
		public Guid OrganizationId { get; set; }
		public Guid WorkstationId { get; set; }
		public Guid UserId { get; set; }
		public Guid UserSessionId { get; set; }
		public Guid ApplicationId { get; set; }
		public Guid ApplicationSessionId { get; set; }
		public Guid VaultEntryId { get; set; }
		public Guid? WindowsBindingId { get; set; }
		public int? WindowsSessionId { get; set; }
		public string AgentCertificateThumbprint { get; set; } = string.Empty;
		public byte[] TokenHash { get; set; } = Array.Empty<byte>();
		public DateTime CreatedAt { get; set; }
		public DateTime ExpiresAt { get; set; }
		public DateTime? ConsumedAt { get; set; }
		public DateTime? RevokedAt { get; set; }
	}

	public class Organization
	{
		public Guid Id { get; set; }
		public string Name { get; set; } = string.Empty;
		public string Slug { get; set; } = string.Empty;
		public bool IsActive { get; set; } = true;
		public DateTime CreatedAt { get; set; }
		public DateTime UpdatedAt { get; set; }
	}

	/// <summary>Durable intent for a host-global settings update awaiting atomic file application.</summary>
	public class GlobalSettingsOperation
	{
		public Guid Id { get; set; }
		public long Sequence { get; set; }
		public Guid OrganizationId { get; set; }
		public string SettingsJson { get; set; } = string.Empty;
		public string Status { get; set; } = "Pending";
		public DateTime CreatedAt { get; set; }
		public DateTime? CompletedAt { get; set; }
	}

	public class Site
	{
		public Guid Id { get; set; }
		public Guid OrganizationId { get; set; }
		public string Name { get; set; } = string.Empty;
		public string? Code { get; set; }
		public bool IsActive { get; set; } = true;
		public DateTime CreatedAt { get; set; }
	}

	public class SecurityGroup
	{
		public Guid Id { get; set; }
		public Guid OrganizationId { get; set; }
		public string Name { get; set; } = string.Empty;
		public string? Description { get; set; }
		public bool IsActive { get; set; } = true;
		public DateTime CreatedAt { get; set; }
	}

	public class RoleDefinition
	{
		public Guid Id { get; set; }
		public Guid OrganizationId { get; set; }
		public string Name { get; set; } = string.Empty;
		public string? Description { get; set; }
		public bool IsSystemRole { get; set; }
		public bool IsActive { get; set; } = true;
		public DateTime CreatedAt { get; set; }
	}

	public class PermissionDefinition
	{
		public Guid Id { get; set; }
		public string Key { get; set; } = string.Empty;
		public string Description { get; set; } = string.Empty;
	}

	public class RolePermission
	{
		public Guid RoleId { get; set; }
		public Guid PermissionId { get; set; }
		public Guid OrganizationId { get; set; }
	}

	public class UserGroupMembership
	{
		public Guid UserId { get; set; }
		public Guid GroupId { get; set; }
		public Guid OrganizationId { get; set; }
	}

	public class GroupRoleMembership
	{
		public Guid GroupId { get; set; }
		public Guid RoleId { get; set; }
		public Guid OrganizationId { get; set; }
	}

	public class UserRoleMembership
	{
		public Guid UserId { get; set; }
		public Guid RoleId { get; set; }
		public Guid OrganizationId { get; set; }
	}

	public class VaultEntry
	{
		public Guid Id { get; set; }
		public Guid OrganizationId { get; set; }
		public Guid? SiteId { get; set; }
		public Guid? UserId { get; set; }
		public Guid ApplicationId { get; set; }
		public string Name { get; set; } = string.Empty;
		public string CredentialType { get; set; } = "password";
		public string Username { get; set; } = string.Empty;
		public byte[] EncryptedSecret { get; set; } = Array.Empty<byte>();
		public byte[] Nonce { get; set; } = Array.Empty<byte>();
		public int SecretVersion { get; set; } = 1;
		public string Status { get; set; } = "active";
		public DateTime CreatedAt { get; set; }
		public DateTime UpdatedAt { get; set; }
		public DateTime? LastRotatedAt { get; set; }
		public DateTime? NextRotationAt { get; set; }
		public DateTime? RevokedAt { get; set; }
		public DateTime? LastUsedAt { get; set; }
		public DateTime? ExpiresAt { get; set; }
		public Guid? CreatedBy { get; set; }
		public Guid? UpdatedBy { get; set; }
		public uint Version { get; set; }
	}
}
