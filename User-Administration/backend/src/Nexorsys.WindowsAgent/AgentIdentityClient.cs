using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography.X509Certificates;
using Nexorsys.Agent.Core;

namespace Nexorsys.WindowsAgent;

public sealed record AgentSessionBindingResponse(Guid Id, Guid UserSessionId, int WindowsSessionId, DateTime ExpiresAt);

public sealed class AgentIdentityClient : IDisposable
{
	private readonly HttpClient _http;

	public AgentIdentityClient(AgentRuntimeConfiguration configuration, X509Certificate2 certificate)
	{
		var handler = new HttpClientHandler { AllowAutoRedirect = false, CheckCertificateRevocationList = true,
			ClientCertificateOptions = ClientCertificateOption.Manual };
		handler.ClientCertificates.Add(certificate);
		_http = new HttpClient(handler, disposeHandler: true) { BaseAddress = configuration.IdentityApiBaseUri, Timeout = TimeSpan.FromSeconds(8) };
	}

	public async Task<AgentSessionBindingResponse> BindWindowsSessionAsync(string sid, int windowsSessionId, CancellationToken cancellationToken)
	{
		using var response = await _http.PostAsJsonAsync("api/agent/v1/vault-release/windows-sessions/bind",
			new BindWindowsSessionRequest(sid, windowsSessionId), cancellationToken);
		return await ReadRequired<AgentSessionBindingResponse>(response, cancellationToken);
	}

	public async Task<AgentLaunchDescriptor> AuthorizeApplicationLaunchAsync(Guid applicationSessionId,
		Guid windowsBindingId, int windowsSessionId,
		CancellationToken cancellationToken)
	{
		using var response = await _http.PostAsJsonAsync("api/agent/v1/vault-release/application-sessions/validate",
			new ValidateAgentApplicationSessionRequest(applicationSessionId, windowsBindingId, windowsSessionId), cancellationToken);
		return await ReadRequired<AgentLaunchDescriptor>(response, cancellationToken);
	}

	public async Task EndWindowsSessionAsync(string sid, int windowsSessionId, CancellationToken cancellationToken)
	{
		using var response = await _http.PostAsJsonAsync("api/agent/v1/vault-release/windows-sessions/end",
			new BindWindowsSessionRequest(sid, windowsSessionId), cancellationToken);
		if (response.StatusCode != HttpStatusCode.NoContent) throw new HttpRequestException("Agent session termination was rejected.", null, response.StatusCode);
	}

	private static async Task<T> ReadRequired<T>(HttpResponseMessage response, CancellationToken cancellationToken)
	{
		if (!response.IsSuccessStatusCode) throw new HttpRequestException("Agent Identity trust request was rejected.", null, response.StatusCode);
		return await response.Content.ReadFromJsonAsync<T>(cancellationToken: cancellationToken)
			?? throw new HttpRequestException("Agent Identity trust response was empty.");
	}

	public void Dispose() => _http.Dispose();
	private sealed record BindWindowsSessionRequest(string WindowsSid, int WindowsSessionId);
	private sealed record ValidateAgentApplicationSessionRequest(Guid ApplicationSessionId, Guid WindowsBindingId, int WindowsSessionId);
}
