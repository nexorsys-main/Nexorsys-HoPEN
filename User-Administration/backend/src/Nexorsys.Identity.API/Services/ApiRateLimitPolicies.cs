using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;

namespace Nexorsys.Identity.API.Services;

public static class ApiRateLimitPolicies
{
	public const string Login = "LoginLimiter";
	public const string Kiosk = "KioskLimiter";

	public static void Configure(RateLimiterOptions options)
	{
		options.AddPolicy(Login, CreateLoginPartition);
		options.AddPolicy(Kiosk, CreateKioskPartition);
	}

	public static RateLimitPartition<string> CreateLoginPartition(HttpContext context) => CreatePerIpPartition(context, permitLimit: 5);

	public static RateLimitPartition<string> CreateKioskPartition(HttpContext context) => CreatePerIpPartition(context, permitLimit: 100);

	private static RateLimitPartition<string> CreatePerIpPartition(HttpContext context, int permitLimit)
	{
		var partitionKey = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
		return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new FixedWindowRateLimiterOptions
		{
			AutoReplenishment = true,
			PermitLimit = permitLimit,
			QueueLimit = 0,
			Window = TimeSpan.FromMinutes(1)
		});
	}
}
