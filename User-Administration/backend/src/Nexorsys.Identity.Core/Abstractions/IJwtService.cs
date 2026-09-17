using System.Security.Claims;
using Nexorsys.Identity.Core;

namespace Nexorsys.Identity.Core.Abstractions
{
	public interface IJwtService
	{
		string GenerateToken(User user);
		ClaimsPrincipal? ValidateToken(string token);
	}
}
