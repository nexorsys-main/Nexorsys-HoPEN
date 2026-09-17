using System.Threading.Tasks;
using Nexorsys.Identity.Core.Abstractions;

namespace Nexorsys.Identity.API.Services.Authentication
{
	public class PsiProvider : IAuthenticationProvider
	{
		public string ProviderType => "PSI";
		public int Priority => 10;

		public Task<AuthResult> AuthenticateAsync(AuthContext context)
		{
			// Stub for Pro Santé Connect (PSI) OpenID Connect flow
			// In reality, this would validate the ID Token issued by Pro Santé Connect

			if (string.IsNullOrEmpty(context.Token))
			{
				return Task.FromResult(new AuthResult { IsSuccess = false, ErrorMessage = "PSI Token required." });
			}

			return Task.FromResult(new AuthResult { IsSuccess = false, ErrorMessage = "Pro Santé Connect integration is not configured." });
		}

		public Task<bool> ValidateAsync(ValidationContext context)
		{
			return Task.FromResult(false);
		}

		public Task<UserProfile> GetProfileAsync(string subjectId)
		{
			return Task.FromException<UserProfile>(new NotSupportedException("Pro Santé Connect profile resolution requires a validated provider integration."));
		}
	}
}
