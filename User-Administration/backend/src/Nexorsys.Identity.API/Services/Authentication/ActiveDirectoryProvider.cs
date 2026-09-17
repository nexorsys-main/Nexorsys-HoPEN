using System.Threading.Tasks;
using Nexorsys.Identity.Core.Abstractions;

namespace Nexorsys.Identity.API.Services.Authentication
{
	public class ActiveDirectoryProvider : IAuthenticationProvider
	{
		public string ProviderType => "ActiveDirectory";
		public int Priority => 3;

		public Task<AuthResult> AuthenticateAsync(AuthContext context)
		{
			// Stub for Active Directory Integration
			return Task.FromResult(new AuthResult { IsSuccess = false, ErrorMessage = "Not implemented" });
		}

		public Task<bool> ValidateAsync(ValidationContext context)
		{
			return Task.FromResult(false);
		}

		public Task<UserProfile> GetProfileAsync(string subjectId)
		{
			return Task.FromException<UserProfile>(new NotSupportedException("Directory profile resolution is not configured in this provider."));
		}
	}
}
