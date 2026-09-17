using System.Threading.Tasks;
using Nexorsys.Identity.Core.Abstractions;

namespace Nexorsys.Identity.API.Services.Authentication
{
	public class ECpsProvider : IAuthenticationProvider
	{
		public string ProviderType => "eCPS";
		public int Priority => 11;

		public Task<AuthResult> AuthenticateAsync(AuthContext context)
		{
			// Stub for e-CPS mobile application authentication flow

			if (string.IsNullOrEmpty(context.Token))
			{
				return Task.FromResult(new AuthResult { IsSuccess = false, ErrorMessage = "e-CPS Token required." });
			}

			return Task.FromResult(new AuthResult { IsSuccess = false, ErrorMessage = "e-CPS provider integration is not configured." });
		}

		public Task<bool> ValidateAsync(ValidationContext context)
		{
			return Task.FromResult(false);
		}

		public Task<UserProfile> GetProfileAsync(string subjectId)
		{
			return Task.FromException<UserProfile>(new NotSupportedException("e-CPS profile resolution requires a validated provider integration."));
		}
	}
}
