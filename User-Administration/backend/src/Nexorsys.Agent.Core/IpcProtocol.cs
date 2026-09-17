using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Nexorsys.Agent.Core;

public sealed record AgentIpcRequest(
    int Version,
    Guid RequestId,
    string Operation,
    DateTimeOffset IssuedAt,
    DateTimeOffset ExpiresAt,
    Guid ApplicationSessionId);

public sealed record AgentLaunchDescriptor(Guid ApplicationSessionId, Guid ApplicationId, string ExecutablePath,
    string PublisherThumbprint, string ExecutableSha256, DateTimeOffset ExpiresAt);

public sealed record AgentIpcResponse(int Version, Guid RequestId, bool Accepted, string Code,
    AgentLaunchDescriptor? Launch = null);

public sealed record WindowsCaller(string Sid, int SessionId, int ProcessId, string ExecutablePath, string IntegrityLevel,
	bool AuthenticodeTrusted, string? PublisherThumbprint, string? ExecutableSha256);

public static class IpcProtocol
{
    public const int CurrentVersion = 2;
    public const int MaxMessageBytes = 16 * 1024;
    public static readonly TimeSpan MaxLifetime = TimeSpan.FromSeconds(15);
    public static readonly TimeSpan ClockSkew = TimeSpan.FromSeconds(5);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    public static bool TryParse(ReadOnlySpan<byte> bytes, DateTimeOffset now, out AgentIpcRequest? request, out string failure)
    {
        request = null;
        failure = "MALFORMED";
        if (bytes.Length is 0 or > MaxMessageBytes) { failure = "SIZE_LIMIT"; return false; }
        try
        {
            using var document = JsonDocument.Parse(bytes.ToArray());
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !HasUtcTimestamp(root, "issuedAt") || !HasUtcTimestamp(root, "expiresAt"))
            { failure = "INVALID_TIMESTAMP"; return false; }
            var parsed = JsonSerializer.Deserialize<AgentIpcRequest>(bytes, JsonOptions);
            if (parsed is null || parsed.Version != CurrentVersion || parsed.RequestId == Guid.Empty ||
                parsed.Operation is not ("vault.release" or "application.launch") ||
                parsed.ApplicationSessionId == Guid.Empty ||
                parsed.IssuedAt.Offset != TimeSpan.Zero || parsed.ExpiresAt.Offset != TimeSpan.Zero ||
                parsed.IssuedAt > now + ClockSkew || parsed.IssuedAt < now - MaxLifetime - ClockSkew ||
                parsed.ExpiresAt <= now || parsed.ExpiresAt <= parsed.IssuedAt || parsed.ExpiresAt - parsed.IssuedAt > MaxLifetime)
            {
                failure = parsed?.Version != CurrentVersion ? "UNSUPPORTED_VERSION" : "INVALID_REQUEST";
                return false;
            }
            request = parsed;
            failure = "OK";
            return true;
        }
        catch (JsonException) { return false; }
    }

    private static bool HasUtcTimestamp(JsonElement root, string name) => root.TryGetProperty(name, out var value) &&
        value.ValueKind == JsonValueKind.String && value.GetString() is { } text && text.EndsWith('Z') &&
        DateTimeOffset.TryParse(text, System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out var timestamp) && timestamp.Offset == TimeSpan.Zero;

    public static byte[] Serialize(AgentIpcRequest request)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteNumber("version", request.Version);
            writer.WriteString("requestId", request.RequestId);
            writer.WriteString("operation", request.Operation);
            writer.WriteString("issuedAt", request.IssuedAt.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'", System.Globalization.CultureInfo.InvariantCulture));
            writer.WriteString("expiresAt", request.ExpiresAt.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'", System.Globalization.CultureInfo.InvariantCulture));
            writer.WriteString("applicationSessionId", request.ApplicationSessionId);
            writer.WriteEndObject();
        }
        return stream.ToArray();
    }
}

/// <summary>Bounded in-memory replay defense. Expired entries are removed; a full cache rejects new requests until expiry frees capacity.</summary>
public sealed class ReplayCache
{
    private readonly int _capacity;
    private readonly ConcurrentDictionary<string, DateTimeOffset> _entries = new(StringComparer.Ordinal);
    private readonly object _gate = new();

    public ReplayCache(int capacity = 20_000)
    {
        if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity));
        _capacity = capacity;
    }

    public bool TryUse(string principalSid, AgentIpcRequest request, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(principalSid) || request.RequestId == Guid.Empty || request.ExpiresAt <= now) return false;
        var key = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(
            $"{principalSid}\n{request.Operation}\n{request.RequestId:D}")));
        lock (_gate)
        {
            foreach (var item in _entries.Where(x => x.Value <= now).ToArray()) _entries.TryRemove(item.Key, out _);
            if (_entries.ContainsKey(key)) return false;
            if (_entries.Count >= _capacity) return false;
            return _entries.TryAdd(key, request.ExpiresAt);
        }
    }
}

public sealed record ApprovedApplication(string ApplicationId, string InstallRoot, string PublisherThumbprint);

public interface IApplicationIdentityPolicy
{
    bool IsApproved(ApprovedApplication policy, string observedExecutablePath, string observedPublisherThumbprint);
}

/// <summary>Path and publisher policy primitive; callers must obtain both observed values from the OS, never IPC fields.</summary>
public sealed class StrictApplicationIdentityPolicy : IApplicationIdentityPolicy
{
    public bool IsApproved(ApprovedApplication policy, string observedExecutablePath, string observedPublisherThumbprint)
    {
        if (string.IsNullOrWhiteSpace(policy.ApplicationId) || string.IsNullOrWhiteSpace(policy.InstallRoot) ||
            string.IsNullOrWhiteSpace(policy.PublisherThumbprint) || string.IsNullOrWhiteSpace(observedExecutablePath) ||
            string.IsNullOrWhiteSpace(observedPublisherThumbprint)) return false;
        try
        {
            var root = Path.GetFullPath(policy.InstallRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var executable = Path.GetFullPath(observedExecutablePath);
            return executable.StartsWith(root, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(NormalizeThumbprint(policy.PublisherThumbprint), NormalizeThumbprint(observedPublisherThumbprint), StringComparison.Ordinal);
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or NotSupportedException) { return false; }
    }

    private static string NormalizeThumbprint(string value) => new(value.Where(Uri.IsHexDigit).Select(char.ToUpperInvariant).ToArray());
}
