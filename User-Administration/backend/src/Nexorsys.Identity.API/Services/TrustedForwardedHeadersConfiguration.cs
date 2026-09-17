using System.Net;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Nexorsys.Identity.API.Services;

public static class TrustedForwardedHeadersConfiguration
{
	public const string SectionName = "ReverseProxy";

	/// <summary>
	/// Enables forwarded client IP/protocol handling only when explicit proxy IPs are configured.
	/// Returns false without registering middleware options when no proxy is configured.
	/// </summary>
	public static bool Configure(IConfiguration configuration, IServiceCollection services)
	{
		var section = configuration.GetSection(SectionName);
		var configuredProxies = section.GetSection("KnownProxies").Get<string[]>() ?? Array.Empty<string>();
		if (configuredProxies.Length == 0)
			return false;

		var proxies = configuredProxies.Select(value =>
			IPAddress.TryParse(value, out var address)
				? address
				: throw new InvalidOperationException($"{SectionName}:KnownProxies must contain only valid IP addresses."))
			.Distinct()
			.ToArray();
		if (proxies.Length == 0)
			return false;

		var forwardLimit = section.GetValue<int?>("ForwardLimit") ?? 1;
		if (forwardLimit is < 1 or > 5)
			throw new InvalidOperationException($"{SectionName}:ForwardLimit must be between 1 and 5.");

		services.Configure<ForwardedHeadersOptions>(options =>
		{
			options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
			options.ForwardLimit = forwardLimit;
			options.RequireHeaderSymmetry = true;
			options.KnownProxies.Clear();
			options.KnownNetworks.Clear();
			foreach (var proxy in proxies)
				options.KnownProxies.Add(proxy);
		});
		return true;
	}
}
