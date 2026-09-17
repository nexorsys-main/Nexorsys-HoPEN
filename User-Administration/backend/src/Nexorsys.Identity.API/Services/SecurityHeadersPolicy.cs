using Microsoft.Extensions.Hosting;

namespace Nexorsys.Identity.API.Services;

/// <summary>Builds response security headers without embedding customer or developer network addresses.</summary>
public static class SecurityHeadersPolicy
{
	public static string BuildContentSecurityPolicy(IHostEnvironment environment, IEnumerable<string>? configuredConnectSources = null)
	{
		var connectSources = new List<string> { "'self'" };
		if (environment.IsDevelopment())
		{
			connectSources.Add("http://localhost:5000");
			connectSources.Add("ws://localhost:5000");
		}

		foreach (var source in configuredConnectSources ?? Enumerable.Empty<string>())
		{
			if (IsAllowedConnectSource(source, environment)) connectSources.Add(source.Trim());
		}

		return $"default-src 'self'; connect-src {string.Join(' ', connectSources.Distinct(StringComparer.OrdinalIgnoreCase))}; frame-ancestors 'none';";
	}

	private static bool IsAllowedConnectSource(string? source, IHostEnvironment environment)
	{
		if (string.IsNullOrWhiteSpace(source) || !Uri.TryCreate(source.Trim(), UriKind.Absolute, out var uri) ||
			(uri.PathAndQuery.Length > 1) || !string.IsNullOrEmpty(uri.Fragment) || !string.IsNullOrEmpty(uri.UserInfo)) return false;

		if (environment.IsDevelopment() && uri.IsLoopback && uri.Scheme is "http" or "https" or "ws" or "wss") return true;
		return uri.Scheme is "https" or "wss";
	}
}
