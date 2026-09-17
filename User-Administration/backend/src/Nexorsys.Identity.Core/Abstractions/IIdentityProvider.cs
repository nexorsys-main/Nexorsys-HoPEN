using Nexorsys.Identity.Core;

namespace Nexorsys.Identity.Core.Abstractions
{
	/// <summary>
	/// Modular identity provider interface for healthcare federation.
	/// Existing NFC + PIN authentication is wrapped as the default provider.
	/// PSI, e-CPS, and OAuth providers implement this same contract.
	/// </summary>
	public interface IIdentityProvider
	{
		string ProviderType { get; }
		string DisplayName { get; }

		Task<ProviderAuthResult> AuthenticateAsync(ProviderAuthRequest request);
		Task<bool> ValidateTokenAsync(string token);
		Task<ProviderUserProfile?> GetUserProfileAsync(string externalId);
		Task<ProviderHealthStatus> CheckHealthAsync();
	}

	public class ProviderAuthRequest
	{
		public string? BadgeUid { get; set; }
		public string? Pin { get; set; }
		public string? Username { get; set; }
		public string? Password { get; set; }
		public string? AuthorizationCode { get; set; }
		public string? RedirectUri { get; set; }
		public string? ExternalToken { get; set; }
	}

	public class ProviderAuthResult
	{
		public bool IsSuccess { get; set; }
		public string? Token { get; set; }
		public string? ExternalSubjectId { get; set; }
		public string AssuranceLevel { get; set; } = "Standard";
		public string? ErrorMessage { get; set; }
		public User? User { get; set; }
	}

	public class ProviderUserProfile
	{
		public string ExternalId { get; set; } = string.Empty;
		public string? DisplayName { get; set; }
		public string? Email { get; set; }
		public string? RppsNumber { get; set; }
		public string? Department { get; set; }
	}

	public class ProviderHealthStatus
	{
		public string Status { get; set; } = "unknown"; // healthy, degraded, offline
		public string? Message { get; set; }
		public DateTime CheckedAt { get; set; } = DateTime.UtcNow;
	}
}
