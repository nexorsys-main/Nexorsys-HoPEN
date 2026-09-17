using System.Threading.Tasks;
using Nexorsys.Identity.Core.Abstractions;

namespace Nexorsys.Identity.API.Services.Authentication
{
	public class WindowsProvider : IAuthenticationProvider
	{
		public string ProviderType => "Windows";
		public int Priority => 2;

		public Task<AuthResult> AuthenticateAsync(AuthContext context)
		{
			// Stub for Windows Session Auth
			return Task.FromResult(new AuthResult { IsSuccess = false, ErrorMessage = "Not implemented" });
		}

		public Task<bool> ValidateAsync(ValidationContext context)
		{
			return Task.FromResult(false);
		}

		public Task<UserProfile> GetProfileAsync(string subjectId)
		{
			return Task.FromException<UserProfile>(new NotSupportedException("Windows profile resolution requires verified Agent integration."));
		}
	}
}
