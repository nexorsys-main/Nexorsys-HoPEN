using System;
using System.Buffers.Binary;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;
using Nexorsys.Agent.Core;

namespace Nexorsys.NFC.APP.Services;

public sealed class AgentLaunchAuthorizationClient : IAgentLaunchAuthorizationClient
{
    private const string PipeName = "Nexorsys.Identity.Agent.v1";
    private const string RegistryPath = @"SOFTWARE\NexorSys\Identity\Agent";

    public async Task<AgentLaunchDescriptor?> AuthorizeAsync(Guid applicationSessionId, CancellationToken cancellationToken = default)
    {
        if (!OperatingSystem.IsWindows() || applicationSessionId == Guid.Empty) return null;
        (string Path, string Publisher, string Sha256)? trustedAgent;
        try { trustedAgent = ReadTrustedAgentConfiguration(); }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or System.Security.SecurityException) { return null; }
        if (trustedAgent is null) return null;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        await using var pipe = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut,
            PipeOptions.Asynchronous, TokenImpersonationLevel.Identification);
        try
        {
            await pipe.ConnectAsync(1500, timeout.Token);
            if (!IsTrustedAgentServer(pipe.SafePipeHandle, trustedAgent.Value)) return null;
            var now = DateTimeOffset.UtcNow;
            var request = new AgentIpcRequest(IpcProtocol.CurrentVersion, Guid.NewGuid(), "application.launch",
                now, now.AddSeconds(10), applicationSessionId);
            var bytes = IpcProtocol.Serialize(request);
            var header = new byte[4];
            BinaryPrimitives.WriteInt32LittleEndian(header, bytes.Length);
            await pipe.WriteAsync(header, timeout.Token);
            await pipe.WriteAsync(bytes, timeout.Token);
            await pipe.FlushAsync(timeout.Token);
            await pipe.ReadExactlyAsync(header, timeout.Token);
            var length = BinaryPrimitives.ReadInt32LittleEndian(header);
            if (length is <= 0 or > IpcProtocol.MaxMessageBytes) return null;
            var responseBytes = new byte[length];
            await pipe.ReadExactlyAsync(responseBytes, timeout.Token);
            var response = JsonSerializer.Deserialize<AgentIpcResponse>(responseBytes,
                new JsonSerializerOptions(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = true });
            if (response is null || response.Version != IpcProtocol.CurrentVersion || response.RequestId != request.RequestId ||
                !response.Accepted || response.Code != "LAUNCH_AUTHORIZED" || response.Launch is null ||
                response.Launch.ApplicationSessionId != applicationSessionId || response.Launch.ExpiresAt <= DateTimeOffset.UtcNow ||
                !IsTrustedExecutable(response.Launch)) return null;
            return response.Launch;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or OperationCanceledException or
            JsonException or Win32Exception or CryptographicException or InvalidOperationException)
        {
            return null;
        }
    }

    private static (string Path, string Publisher, string Sha256)? ReadTrustedAgentConfiguration()
    {
        using var root = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
        using var settings = root.OpenSubKey(RegistryPath, writable: false);
        var path = settings?.GetValue("AgentExecutablePath") as string;
        var publisher = NormalizeHex(settings?.GetValue("AgentPublisherThumbprint") as string);
        var sha256 = NormalizeHex(settings?.GetValue("AgentExecutableSha256") as string);
        if (string.IsNullOrWhiteSpace(path) || publisher.Length != 40 || !publisher.All(Uri.IsHexDigit) ||
            sha256.Length != 64 || !sha256.All(Uri.IsHexDigit) || !Path.IsPathFullyQualified(path)) return null;
        var fullPath = Path.GetFullPath(path);
        var pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        if (string.IsNullOrWhiteSpace(pf) || !fullPath.StartsWith(Path.GetFullPath(pf).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase)) return null;
        return (fullPath, publisher, sha256);
    }

    private static bool IsTrustedAgentServer(SafePipeHandle pipe, (string Path, string Publisher, string Sha256) expected)
    {
        if (!GetNamedPipeServerProcessId(pipe, out var processId) || processId == 0) return false;
        try
        {
            using var process = Process.GetProcessById(checked((int)processId));
            var path = Path.GetFullPath(process.MainModule?.FileName ?? "");
            if (!string.Equals(path, expected.Path, StringComparison.OrdinalIgnoreCase)) return false;
            using var image = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            var actualHash = Convert.ToHexString(SHA256.HashData(image));
            return CryptographicOperations.FixedTimeEquals(Convert.FromHexString(actualHash), Convert.FromHexString(expected.Sha256)) &&
                IsTrustedCodeSigningImage(path, expected.Publisher);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or Win32Exception or IOException) { return false; }
    }

    private static bool IsTrustedExecutable(AgentLaunchDescriptor launch)
    {
        if (!Path.IsPathFullyQualified(launch.ExecutablePath) || !string.Equals(Path.GetExtension(launch.ExecutablePath), ".exe", StringComparison.OrdinalIgnoreCase) ||
            launch.ExecutableSha256.Length != 64 || !launch.ExecutableSha256.All(Uri.IsHexDigit) || launch.ExpiresAt <= DateTimeOffset.UtcNow) return false;
        try
        {
            var path = Path.GetFullPath(launch.ExecutablePath);
            var programFiles = new[] { Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86) };
            if (!string.Equals(path, launch.ExecutablePath, StringComparison.OrdinalIgnoreCase) || !File.Exists(path) ||
                !Array.Exists(programFiles, root => !string.IsNullOrWhiteSpace(root) &&
                    path.StartsWith(Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))) return false;
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            var hash = SHA256.HashData(stream);
            var expectedHash = Convert.FromHexString(launch.ExecutableSha256);
            return CryptographicOperations.FixedTimeEquals(hash, expectedHash) && IsTrustedCodeSigningImage(path, NormalizeThumbprint(launch.PublisherThumbprint));
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException or FormatException or CryptographicException or NotSupportedException) { return false; }
    }

    private static bool IsTrustedCodeSigningImage(string path, string expectedThumbprint)
    {
        try
        {
            using var certificate = new X509Certificate2(X509Certificate.CreateFromSignedFile(path));
            var actual = NormalizeThumbprint(certificate.Thumbprint);
            var actualBytes = System.Text.Encoding.ASCII.GetBytes(actual);
            var expectedBytes = System.Text.Encoding.ASCII.GetBytes(expectedThumbprint);
            if (actualBytes.Length != 40 || expectedBytes.Length != 40 || !CryptographicOperations.FixedTimeEquals(actualBytes, expectedBytes)) return false;
            using var chain = new X509Chain();
            chain.ChainPolicy.RevocationMode = X509RevocationMode.Online;
            chain.ChainPolicy.RevocationFlag = X509RevocationFlag.ExcludeRoot;
            chain.ChainPolicy.ApplicationPolicy.Add(new Oid("1.3.6.1.5.5.7.3.3"));
            return certificate.NotBefore.ToUniversalTime() <= DateTime.UtcNow && certificate.NotAfter.ToUniversalTime() > DateTime.UtcNow && chain.Build(certificate);
        }
        catch (CryptographicException) { return false; }
    }

    private static string NormalizeThumbprint(string? value) => string.IsNullOrWhiteSpace(value)
        ? string.Empty : new string(value.Where(Uri.IsHexDigit).Select(char.ToUpperInvariant).ToArray());

    private static string NormalizeHex(string? value) => string.IsNullOrWhiteSpace(value)
        ? string.Empty : value.Any(c => !Uri.IsHexDigit(c) && !char.IsWhiteSpace(c) && c != ':')
            ? string.Empty : new string(value.Where(Uri.IsHexDigit).Select(char.ToUpperInvariant).ToArray());

    [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetNamedPipeServerProcessId(SafePipeHandle pipe, out uint serverProcessId);
}
