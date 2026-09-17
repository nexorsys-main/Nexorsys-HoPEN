using System;
using System.Threading.Tasks;

namespace Nexorsys.Identity.Core.Abstractions
{
	public interface IAuthenticationProvider
	{
		string ProviderType { get; }
		int Priority { get; }

		Task<AuthResult> AuthenticateAsync(AuthContext context);
		Task<bool> ValidateAsync(ValidationContext context);
		Task<UserProfile> GetProfileAsync(string subjectId);
	}

	public class AuthContext
	{
		public string? BadgeUid { get; set; }
		public string? Pin { get; set; }
		public string? Username { get; set; }
		public string? Password { get; set; }
		public string? DeviceCertificateThumbprint { get; set; }
		public string? WorkstationHostname { get; set; }
		public string? Token { get; set; }
	}

	public class ValidationContext
	{
		public string Token { get; set; } = string.Empty;
		public string? DeviceCertificateThumbprint { get; set; }
	}

	public class AuthResult
	{
		public bool IsSuccess { get; set; }
		public string? Token { get; set; }
		public string? ErrorMessage { get; set; }
		public UserProfile? Profile { get; set; }
	}

	public class UserProfile
	{
		public string SubjectId { get; set; } = string.Empty;
		public string? Username { get; set; }
		public string? DisplayName { get; set; }
	}
}
