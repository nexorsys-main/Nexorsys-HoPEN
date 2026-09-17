using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Text.Json.Serialization;
using Nexorsys.Identity.Core;

namespace Nexorsys.Identity.API.Services;

public sealed record RevocationSnapshotStoreResult(bool Accepted, string Code);

/// <summary>Validates snapshots before atomic persistence and rejects sequence rollback.</summary>
public sealed class SignedLicenseRevocationSnapshotStore(IConfiguration configuration, TimeProvider clock, ILogger<SignedLicenseRevocationSnapshotStore> logger)
{
    private readonly SemaphoreSlim _writeGate = new(1, 1);

    public RevocationSnapshotVerification VerifyCurrentSnapshot() => SignedLicenseRevocationSnapshotVerifier.Verify(
        ReadCurrentSnapshot(), configuration["Licensing:PublicKeyPem"], clock.GetUtcNow());

    public string? ReadCurrentSnapshot()
    {
        var file = configuration["Licensing:RevocationSnapshotFile"];
        if (string.IsNullOrWhiteSpace(file)) return configuration["Licensing:RevocationSnapshot"];
        try
        {
            var path = Path.GetFullPath(file);
            if (!File.Exists(path) || (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) return null;
            var token = File.ReadAllText(path);
            return SignedLicenseRevocationSnapshotVerifier.Verify(token, configuration["Licensing:PublicKeyPem"], clock.GetUtcNow()).IsValid
                ? token : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            logger.LogError("Signed revocation snapshot cache could not be read; fail-closed errorType={ErrorType}", ex.GetType().Name);
            return null;
        }
    }

    public async Task<RevocationSnapshotStoreResult> PersistAsync(string signedSnapshot, CancellationToken cancellationToken)
    {
        var file = configuration["Licensing:RevocationSnapshotFile"];
        if (string.IsNullOrWhiteSpace(file)) return new(false, "REVOCATION_CACHE_PATH_NOT_CONFIGURED");
        var incoming = SignedLicenseRevocationSnapshotVerifier.Verify(signedSnapshot,
            configuration["Licensing:PublicKeyPem"], clock.GetUtcNow());
        if (!incoming.IsValid || incoming.Snapshot is null) return new(false, incoming.Code);

        await _writeGate.WaitAsync(cancellationToken);
        string? temporaryPath = null;
        try
        {
            var destination = Path.GetFullPath(file);
            var directory = Path.GetDirectoryName(destination) ?? throw new InvalidOperationException("Snapshot cache path has no parent directory.");
            Directory.CreateDirectory(directory);
            if (Directory.Exists(destination) || (File.Exists(destination) && (File.GetAttributes(destination) & FileAttributes.ReparsePoint) != 0))
                return new(false, "REVOCATION_CACHE_TARGET_INVALID");

            // The semaphore protects callers sharing this object; the sidecar handle also
            // serializes independent API instances/processes that share the same cache file.
            await using var processLock = await AcquireCrossProcessWriteLockAsync(destination + ".lock", cancellationToken);

            if (File.Exists(destination))
            {
                var currentToken = await File.ReadAllTextAsync(destination, cancellationToken);
                if (!SignedLicenseRevocationSnapshotVerifier.TryReadSignedSequence(currentToken,
                    configuration["Licensing:PublicKeyPem"], out var currentSequence))
                    return new(false, "REVOCATION_CACHE_CURRENT_INVALID");
                if (incoming.Snapshot.Sequence < currentSequence) return new(false, "REVOCATION_SEQUENCE_ROLLBACK");
                if (incoming.Snapshot.Sequence == currentSequence)
                {
                    var current = SignedLicenseRevocationSnapshotVerifier.Verify(currentToken,
                        configuration["Licensing:PublicKeyPem"], clock.GetUtcNow());
                    return current.IsValid && string.Equals(currentToken, signedSnapshot, StringComparison.Ordinal)
                        ? new(true, "REVOCATION_SNAPSHOT_UNCHANGED") : new(false, "REVOCATION_SEQUENCE_CONFLICT");
                }
            }

            temporaryPath = Path.Combine(directory, ".revocation-" + Guid.NewGuid().ToString("N") + ".tmp");
            var bytes = System.Text.Encoding.UTF8.GetBytes(signedSnapshot);
            await using (var stream = new FileStream(temporaryPath, new FileStreamOptions
            {
                Mode = FileMode.CreateNew, Access = FileAccess.Write, Share = FileShare.None,
                Options = FileOptions.Asynchronous | FileOptions.WriteThrough
            }))
            {
                await stream.WriteAsync(bytes, cancellationToken);
                await stream.FlushAsync(cancellationToken);
                stream.Flush(flushToDisk: true);
            }
            File.Move(temporaryPath, destination, overwrite: true);
            temporaryPath = null;
            logger.LogInformation("Signed license revocation snapshot updated; sequence={Sequence}, expiresAt={ExpiresAt}",
                incoming.Snapshot.Sequence, incoming.Snapshot.ExpiresAt);
            return new(true, "REVOCATION_SNAPSHOT_UPDATED");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            logger.LogError("Signed revocation snapshot persistence failed; fail-closed errorType={ErrorType}", ex.GetType().Name);
            return new(false, "REVOCATION_CACHE_WRITE_FAILED");
        }
        finally
        {
            if (temporaryPath is not null && File.Exists(temporaryPath))
            {
                try { File.Delete(temporaryPath); }
                catch (IOException) { logger.LogWarning("A temporary revocation cache file could not be removed."); }
            }
            _writeGate.Release();
        }
    }

    private static async Task<FileStream> AcquireCrossProcessWriteLockAsync(string lockPath, CancellationToken cancellationToken)
    {
        const int attempts = 200;
        for (var attempt = 0; attempt < attempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                if (File.Exists(lockPath) && (File.GetAttributes(lockPath) & FileAttributes.ReparsePoint) != 0)
                    throw new UnauthorizedAccessException("Revocation cache lock file cannot be a reparse point.");
                return new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None,
                    bufferSize: 1, useAsync: true);
            }
            catch (IOException) when (attempt + 1 < attempts)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(25), cancellationToken);
            }
        }

        throw new IOException("Timed out waiting for the revocation cache process lock.");
    }
}

/// <summary>mTLS client for a configurable future license service contract. It contains no vendor signing key.</summary>
public sealed class SignedLicenseRevocationFeedClient : IDisposable
{
    private const int MaximumFeedResponseBytes = 65_536;
    private static readonly JsonSerializerOptions FeedJsonOptions = new(JsonSerializerDefaults.Web)
    {
        MaxDepth = 4,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };
    private readonly HttpClient _http;
    private readonly Uri _feedUri;
    private readonly X509Certificate2 _clientCertificate;

    public SignedLicenseRevocationFeedClient(IConfiguration configuration)
    {
        if (!Uri.TryCreate(configuration["Licensing:RevocationFeed:Url"], UriKind.Absolute, out var feedUri) ||
            feedUri.Scheme != Uri.UriSchemeHttps || feedUri.IsLoopback || !string.IsNullOrEmpty(feedUri.UserInfo) ||
            !string.IsNullOrEmpty(feedUri.Query) || !string.IsNullOrEmpty(feedUri.Fragment))
            throw new InvalidOperationException("A non-loopback HTTPS license revocation feed URL is required.");
        _feedUri = feedUri;
        var thumbprint = Normalize(configuration["Licensing:RevocationFeed:ClientCertificateThumbprint"]);
        if (thumbprint.Length != 40 || !thumbprint.All(Uri.IsHexDigit))
            throw new InvalidOperationException("A provisioned mTLS revocation-feed client certificate thumbprint is required.");

        using var store = new X509Store(StoreName.My, StoreLocation.LocalMachine);
        store.Open(OpenFlags.ReadOnly | OpenFlags.OpenExistingOnly);
        var certificate = store.Certificates.Cast<X509Certificate2>().SingleOrDefault(candidate =>
            Normalize(candidate.Thumbprint) == thumbprint && candidate.HasPrivateKey && candidate.NotBefore.ToUniversalTime() <= DateTime.UtcNow && candidate.NotAfter.ToUniversalTime() > DateTime.UtcNow);
        if (certificate is null) throw new InvalidOperationException("The configured revocation-feed mTLS certificate is not available in LocalMachine\\My.");
        var clientAuthEku = certificate.Extensions.OfType<X509EnhancedKeyUsageExtension>().SingleOrDefault()?.EnhancedKeyUsages
            .Cast<Oid>().Any(oid => oid.Value == "1.3.6.1.5.5.7.3.2") == true;
        if (!clientAuthEku) throw new InvalidOperationException("The configured revocation-feed certificate lacks Client Authentication EKU.");
        _clientCertificate = new X509Certificate2(certificate);
        var handler = new HttpClientHandler { AllowAutoRedirect = false, CheckCertificateRevocationList = true,
            ClientCertificateOptions = ClientCertificateOption.Manual };
        handler.ClientCertificates.Add(_clientCertificate);
        _http = new HttpClient(handler, disposeHandler: true)
        {
            Timeout = TimeSpan.FromSeconds(10), MaxResponseContentBufferSize = 65_536
        };
    }

    public async Task<string> FetchAsync(CancellationToken cancellationToken)
    {
        using var response = await _http.GetAsync(_feedUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (response.StatusCode is HttpStatusCode.MovedPermanently or HttpStatusCode.Found or HttpStatusCode.SeeOther or
            HttpStatusCode.TemporaryRedirect or HttpStatusCode.PermanentRedirect)
            throw new HttpRequestException("Revocation feed redirects are not permitted.");
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength is > MaximumFeedResponseBytes)
            throw new InvalidDataException("Revocation snapshot response exceeds its size limit.");
        await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var responseBytes = await ReadBoundedResponseAsync(responseStream, cancellationToken);
        var payload = JsonSerializer.Deserialize<RevocationFeedResponse>(responseBytes, FeedJsonOptions)
            ?? throw new InvalidDataException("Revocation feed response is empty.");
        if (string.IsNullOrWhiteSpace(payload.SignedSnapshot) || payload.SignedSnapshot.Length > MaximumFeedResponseBytes)
            throw new InvalidDataException("Revocation feed response has no bounded signed snapshot.");
        return payload.SignedSnapshot;
    }

    internal static async Task<byte[]> ReadBoundedResponseAsync(Stream responseStream, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream(capacity: MaximumFeedResponseBytes);
        var chunk = new byte[8192];
        while (true)
        {
            var remainingIncludingOverflowProbe = MaximumFeedResponseBytes + 1 - (int)buffer.Length;
            if (remainingIncludingOverflowProbe <= 0)
                throw new InvalidDataException("Revocation snapshot response exceeds its size limit.");
            var read = await responseStream.ReadAsync(chunk.AsMemory(0,
                Math.Min(chunk.Length, remainingIncludingOverflowProbe)), cancellationToken);
            if (read == 0) break;
            if (buffer.Length + read > MaximumFeedResponseBytes)
                throw new InvalidDataException("Revocation snapshot response exceeds its size limit.");
            buffer.Write(chunk, 0, read);
        }
        return buffer.ToArray();
    }

    public void Dispose() { _http.Dispose(); _clientCertificate.Dispose(); }
    private static string Normalize(string? value) => string.IsNullOrWhiteSpace(value)
        ? string.Empty : new string(value.Where(Uri.IsHexDigit).Select(char.ToUpperInvariant).ToArray());
    private sealed record RevocationFeedResponse(string SignedSnapshot);
}

public sealed class LicenseRevocationRefreshService(SignedLicenseRevocationFeedClient feed,
    SignedLicenseRevocationSnapshotStore store, IConfiguration configuration, ILogger<LicenseRevocationRefreshService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var minutes = configuration.GetValue("Licensing:RevocationFeed:RefreshMinutes", 60);
        if (minutes is < 1 or > 1_440) throw new InvalidOperationException("Revocation feed refresh interval must be between 1 and 1440 minutes.");
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(minutes));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var snapshot = await feed.FetchAsync(stoppingToken);
                var result = await store.PersistAsync(snapshot, stoppingToken);
                if (!result.Accepted) logger.LogError("Revocation snapshot update rejected; code={FailureCode}", result.Code);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) when (ex is HttpRequestException or IOException or JsonException or InvalidOperationException)
            {
                logger.LogError("Revocation feed refresh failed; code={FailureCode}", ex.GetType().Name);
            }
            if (!await timer.WaitForNextTickAsync(stoppingToken)) break;
        }
    }
}
