using System.DirectoryServices.Protocols;
using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Nexorsys.Identity.Core;
using Nexorsys.Identity.Core.Abstractions;

namespace Nexorsys.Identity.Infrastructure;

/// <summary>
/// LDAP integration deliberately supports LDAPS only. Configuration and credentials
/// come from the host's secret provider, never a repository-relative JSON file.
/// </summary>
public sealed class LdapService : ILdapService
{
	private const int OperationTimeoutSeconds = 8;
	private readonly Uri? _endpoint;
	private readonly string _serviceUsername;
	private readonly string _servicePassword;
	private readonly string _baseDn;
	private readonly ILogger<LdapService> _logger;

	public LdapService(IConfiguration configuration, ILogger<LdapService> logger)
	{
		_logger = logger;
		_serviceUsername = configuration["Ldap:ServiceAccount"] ?? string.Empty;
		_servicePassword = configuration["Ldap:ServicePassword"] ?? string.Empty;
		_baseDn = configuration["Ldap:BaseDn"] ?? string.Empty;
		if (Uri.TryCreate(configuration["Ldap:Url"], UriKind.Absolute, out var endpoint) &&
			string.Equals(endpoint.Scheme, "ldaps", StringComparison.OrdinalIgnoreCase) && string.IsNullOrEmpty(endpoint.UserInfo) &&
			string.IsNullOrEmpty(endpoint.Query) && string.IsNullOrEmpty(endpoint.Fragment) &&
			(endpoint.AbsolutePath is "" or "/") && !endpoint.IsLoopback)
			_endpoint = endpoint;
	}

	public Task<bool> TestConnectionAsync()
	{
		try
		{
			using var connection = CreateServiceConnection();
			return Task.FromResult(true);
		}
		catch (Exception ex) when (IsDirectoryFailure(ex))
		{
			_logger.LogWarning("Configured LDAPS service bind failed; errorType={ErrorType}", ex.GetType().Name);
			return Task.FromResult(false);
		}
	}

	public Task<bool> AuthenticateAsync(string username, string password)
	{
		if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password)) return Task.FromResult(false);
		try
		{
			using var connection = CreateConnection();
			connection.Bind(new NetworkCredential(username, password));
			return Task.FromResult(true);
		}
		catch (Exception ex) when (IsDirectoryFailure(ex))
		{
			_logger.LogWarning("LDAPS user bind failed; errorType={ErrorType}", ex.GetType().Name);
			return Task.FromResult(false);
		}
	}

	public async Task<User?> GetUserBySamAccountNameAsync(string samAccountName)
	{
		if (string.IsNullOrWhiteSpace(samAccountName)) return null;
		var filter = $"(&(objectCategory=person)(objectClass=user)(sAMAccountName={EscapeLdapFilter(samAccountName)}))";
		return (await SearchLdapUsersAsync(filter)).FirstOrDefault();
	}

	public async Task<User?> GetUserByBadgeUidAsync(string badgeUid)
	{
		if (string.IsNullOrWhiteSpace(badgeUid)) return null;
		var filter = $"(&(objectCategory=person)(objectClass=user)(extensionAttribute1={EscapeLdapFilter(badgeUid)}))";
		return (await SearchLdapUsersAsync(filter)).FirstOrDefault();
	}

	public Task<IEnumerable<User>> SearchUsersAsync(string query)
	{
		var filter = "(&(objectCategory=person)(objectClass=user)";
		if (!string.IsNullOrWhiteSpace(query))
		{
			var escaped = EscapeLdapFilter(query);
			filter += $"(|(sAMAccountName=*{escaped}*)(displayName=*{escaped}*)(mail=*{escaped}*))";
		}
		return SearchLdapUsersAsync(filter + ")");
	}

	public Task<bool> IsUserInGroupAsync(string samAccountName, string groupName)
	{
		if (string.IsNullOrWhiteSpace(samAccountName) || string.IsNullOrWhiteSpace(groupName)) return Task.FromResult(false);
		try
		{
			using var connection = CreateServiceConnection();
			var filter = $"(&(objectCategory=person)(objectClass=user)(sAMAccountName={EscapeLdapFilter(samAccountName)}))";
			var request = new SearchRequest(_baseDn, filter, SearchScope.Subtree, "memberOf");
			var response = (SearchResponse)connection.SendRequest(request);
			if (response.Entries.Count != 1) return Task.FromResult(false);
			var memberships = response.Entries[0].Attributes["memberOf"];
			if (memberships is null) return Task.FromResult(false);
			var matched = memberships.Cast<object>().OfType<string>().Any(value =>
				string.Equals(value, groupName, StringComparison.OrdinalIgnoreCase) ||
				string.Equals(ExtractCn(value), groupName, StringComparison.OrdinalIgnoreCase));
			return Task.FromResult(matched);
		}
		catch (Exception ex) when (IsDirectoryFailure(ex))
		{
			_logger.LogWarning("LDAPS group membership query failed; errorType={ErrorType}", ex.GetType().Name);
			return Task.FromResult(false);
		}
	}

	private Task<IEnumerable<User>> SearchLdapUsersAsync(string filter)
	{
		var users = new List<User>();
		try
		{
			using var connection = CreateServiceConnection();
			var request = new SearchRequest(_baseDn, filter, SearchScope.Subtree,
				"objectGUID", "sAMAccountName", "displayName", "mail", "givenName", "sn", "department", "title", "distinguishedName", "userAccountControl");
			var response = (SearchResponse)connection.SendRequest(request);
			foreach (SearchResultEntry entry in response.Entries)
			{
				if (entry.Attributes["userAccountControl"] is { Count: > 0 } uac &&
					int.TryParse(uac[0]?.ToString(), out var flags) && (flags & 0x0002) != 0) continue;
				users.Add(new User
				{
					AdGuid = ReadGuid(entry.Attributes["objectGUID"]),
					SamAccountName = ReadString(entry, "sAMAccountName") ?? string.Empty,
					DisplayName = ReadString(entry, "displayName"),
					Email = ReadString(entry, "mail"),
					FirstName = ReadString(entry, "givenName"),
					LastName = ReadString(entry, "sn"),
					Department = ReadString(entry, "department"),
					Title = ReadString(entry, "title"),
					DistinguishedName = entry.DistinguishedName,
					IsActive = true,
					IsLocalProfile = false,
					CreatedAt = DateTime.UtcNow,
					UpdatedAt = DateTime.UtcNow
				});
			}
		}
		catch (Exception ex) when (IsDirectoryFailure(ex))
		{
			_logger.LogWarning("LDAPS directory search failed; errorType={ErrorType}", ex.GetType().Name);
			return Task.FromResult<IEnumerable<User>>(Array.Empty<User>());
		}
		return Task.FromResult<IEnumerable<User>>(users);
	}

	private LdapConnection CreateServiceConnection()
	{
		if (_endpoint is null || string.IsNullOrWhiteSpace(_baseDn) || string.IsNullOrWhiteSpace(_serviceUsername) ||
			string.IsNullOrEmpty(_servicePassword))
			throw new InvalidOperationException("LDAPS host configuration is incomplete.");
		var connection = CreateConnection();
		try
		{
			connection.Bind(new NetworkCredential(_serviceUsername, _servicePassword));
			return connection;
		}
		catch { connection.Dispose(); throw; }
	}

	private LdapConnection CreateConnection()
	{
		if (_endpoint is null) throw new InvalidOperationException("A valid non-loopback LDAPS endpoint is required.");
		var connection = new LdapConnection(new LdapDirectoryIdentifier(_endpoint.DnsSafeHost,
			_endpoint.IsDefaultPort ? 636 : _endpoint.Port, fullyQualifiedDnsHostName: true, connectionless: false))
		{
			AuthType = AuthType.Basic,
			Timeout = TimeSpan.FromSeconds(OperationTimeoutSeconds)
		};
		connection.SessionOptions.ProtocolVersion = 3;
		connection.SessionOptions.SecureSocketLayer = true;
		connection.SessionOptions.ReferralChasing = ReferralChasingOptions.None;
		return connection;
	}

	private static string EscapeLdapFilter(string input)
	{
		var builder = new System.Text.StringBuilder(input.Length);
		foreach (var character in input)
		{
			builder.Append(character switch
			{
				'\\' => "\\5c", '*' => "\\2a", '(' => "\\28", ')' => "\\29", '\0' => "\\00",
				_ => character.ToString()
			});
		}
		return builder.ToString();
	}

	private static string? ReadString(SearchResultEntry entry, string attribute) =>
		entry.Attributes[attribute] is { Count: > 0 } values ? values[0]?.ToString() : null;

	private static string? ReadGuid(DirectoryAttribute? attribute)
	{
		if (attribute is not { Count: > 0 } || attribute[0] is not byte[] bytes || bytes.Length != 16) return null;
		return new Guid(bytes).ToString("D");
	}

	private static string? ExtractCn(string distinguishedName)
	{
		if (!distinguishedName.StartsWith("CN=", StringComparison.OrdinalIgnoreCase)) return null;
		var end = distinguishedName.IndexOf(',');
		return end < 0 ? distinguishedName[3..] : distinguishedName[3..end];
	}

	private static bool IsDirectoryFailure(Exception ex) => ex is LdapException or DirectoryOperationException or
		SocketException or TimeoutException or InvalidOperationException or ArgumentException;
}
