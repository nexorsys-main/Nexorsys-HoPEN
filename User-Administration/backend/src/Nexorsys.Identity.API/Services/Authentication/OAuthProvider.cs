using System.Threading.Tasks;
using Nexorsys.Identity.Core.Abstractions;

namespace Nexorsys.Identity.API.Services.Authentication
{
	public class OAuthProvider : IAuthenticationProvider
	{
		public string ProviderType => "OAuth";
		public int Priority => 4;

		public Task<AuthResult> AuthenticateAsync(AuthContext context)
		{
			// Stub for future OAuth2/OIDC Integration
			return Task.FromResult(new AuthResult { IsSuccess = false, ErrorMessage = "Not implemented" });
		}

		public Task<bool> ValidateAsync(ValidationContext context)
		{
			return Task.FromResult(false);
		}

		public Task<UserProfile> GetProfileAsync(string subjectId)
		{
			return Task.FromException<UserProfile>(new NotSupportedException("OAuth profile resolution requires a configured, validated provider."));
		}
	}
}
