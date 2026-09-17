using System.Buffers.Binary;
using System.Diagnostics;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text.Json;
using Microsoft.Win32.SafeHandles;
using Nexorsys.Agent.Core;
using System.Collections.Concurrent;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace Nexorsys.WindowsAgent;

public sealed class AgentPipeService(ILogger<AgentPipeService> logger, AgentIdentityClient identityClient,
	AgentRuntimeConfiguration configuration) : BackgroundService
{
    private const string PipeName = "Nexorsys.Identity.Agent.v1";
    private readonly ReplayCache _replay = new();
    private readonly SemaphoreSlim _connections = new(16, 16);
    private readonly ConcurrentDictionary<(string Sid, int SessionId), byte> _observedSessions = new();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var monitor = MonitorObservedSessionsAsync(stoppingToken);
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await _connections.WaitAsync(stoppingToken);
                var pipe = CreatePipe();
                try { await pipe.WaitForConnectionAsync(stoppingToken); }
                catch { pipe.Dispose(); _connections.Release(); if (stoppingToken.IsCancellationRequested) break; throw; }
                _ = HandleAsync(pipe, stoppingToken);
            }
        }
        finally { try { await monitor; } catch (OperationCanceledException) { } }
    }

	private async Task MonitorObservedSessionsAsync(CancellationToken stoppingToken)
	{
		using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));
		while (await timer.WaitForNextTickAsync(stoppingToken))
		{
			foreach (var session in _observedSessions.Keys)
			{
				if (WindowsSessionInspector.IsConnectedAndUnlocked(session.SessionId)) continue;
				_observedSessions.TryRemove(session, out _);
				try { await identityClient.EndWindowsSessionAsync(session.Sid, session.SessionId, stoppingToken); }
				catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException)
				{ logger.LogWarning("Agent session lifecycle revoke failed; errorType={ErrorType}", ex.GetType().Name); }
			}
		}
	}

    private static NamedPipeServerStream CreatePipe()
    {
        var security = new PipeSecurity();
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        security.AddAccessRule(new PipeAccessRule(new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null), PipeAccessRights.FullControl, AccessControlType.Allow));
        security.AddAccessRule(new PipeAccessRule(new SecurityIdentifier(WellKnownSidType.AuthenticatedUserSid, null), PipeAccessRights.ReadWrite, AccessControlType.Allow));
        security.AddAccessRule(new PipeAccessRule(new SecurityIdentifier(WellKnownSidType.AnonymousSid, null), PipeAccessRights.FullControl, AccessControlType.Deny));
        return NamedPipeServerStreamAcl.Create(PipeName, PipeDirection.InOut, 16, PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous | PipeOptions.WriteThrough, IpcProtocol.MaxMessageBytes, IpcProtocol.MaxMessageBytes, security);
    }

    private async Task HandleAsync(NamedPipeServerStream pipe, CancellationToken stoppingToken)
    {
        using var requestTimeout = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        requestTimeout.CancelAfter(TimeSpan.FromSeconds(10));
        var requestToken = requestTimeout.Token;
        try
        {
            var caller = WindowsCallerInspector.Inspect(pipe);
            var header = new byte[4];
            await pipe.ReadExactlyAsync(header, requestToken);
            var length = BinaryPrimitives.ReadInt32LittleEndian(header);
            if (length is <= 0 or > IpcProtocol.MaxMessageBytes) { await WriteResponse(pipe, Guid.Empty, false, "SIZE_LIMIT", requestToken); return; }
            var bytes = new byte[length];
            await pipe.ReadExactlyAsync(bytes, requestToken);
            if (!IpcProtocol.TryParse(bytes, DateTimeOffset.UtcNow, out var request, out var failure) || request is null)
            { await WriteResponse(pipe, Guid.Empty, false, failure, requestToken); return; }

            if (caller.ProcessId <= 0 || caller.SessionId < 0 || string.IsNullOrWhiteSpace(caller.Sid) ||
                !_replay.TryUse(caller.Sid, request, DateTimeOffset.UtcNow))
            { await WriteResponse(pipe, request.RequestId, false, "CALLER_OR_REPLAY_DENIED", requestToken); return; }

			if (!ExecutableImageIdentityPolicy.IsAllowed(configuration.KioskExecutablePath, configuration.KioskPublisherThumbprint,
				configuration.KioskExecutableSha256, caller.ExecutablePath, caller.PublisherThumbprint, caller.ExecutableSha256,
				caller.AuthenticodeTrusted))
			{ await WriteResponse(pipe, request.RequestId, false, "KIOSK_IMAGE_NOT_ALLOWLISTED", requestToken); return; }
			if (request.Operation != "application.launch" || request.ApplicationSessionId == Guid.Empty)
			{ await WriteResponse(pipe, request.RequestId, false, "OPERATION_DISABLED", requestToken); return; }
			var sessionKey = (caller.Sid, caller.SessionId);
			if (!_observedSessions.ContainsKey(sessionKey) && _observedSessions.Count >= 256)
			{ await WriteResponse(pipe, request.RequestId, false, "SESSION_TRACKING_CAPACITY", requestToken); return; }

			try
			{
				var binding = await identityClient.BindWindowsSessionAsync(caller.Sid, caller.SessionId, requestToken);
				if (binding.WindowsSessionId != caller.SessionId)
				{
					await WriteResponse(pipe, request.RequestId, false, "IDENTITY_BINDING_MISMATCH", requestToken);
					return;
				}
				_observedSessions.TryAdd(sessionKey, 0);
				var launch = await identityClient.AuthorizeApplicationLaunchAsync(request.ApplicationSessionId, binding.Id,
					caller.SessionId, requestToken);
				if (launch.ApplicationSessionId != request.ApplicationSessionId || launch.ExpiresAt <= DateTimeOffset.UtcNow ||
					!IsApprovedLocalExecutable(launch))
				{
					await WriteResponse(pipe, request.RequestId, false, "REGISTERED_EXECUTABLE_MISMATCH", requestToken);
					return;
				}
				await WriteResponse(pipe, request.RequestId, true, "LAUNCH_AUTHORIZED", requestToken, launch);
				return;
			}
			catch (HttpRequestException ex)
			{
				logger.LogWarning("Agent Identity request denied; status={StatusCode}", (int?)ex.StatusCode);
				await WriteResponse(pipe, request.RequestId, false,
					ex.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden ? "IDENTITY_BINDING_DENIED" : "IDENTITY_API_UNAVAILABLE", requestToken);
				return;
			}

        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
        catch (OperationCanceledException) { }
        catch (Exception ex) { logger.LogWarning("Agent IPC request rejected; errorType={ErrorType}", ex.GetType().Name); }
        finally { pipe.Dispose(); _connections.Release(); }
    }

	public override async Task StopAsync(CancellationToken cancellationToken)
	{
		foreach (var session in _observedSessions.Keys.ToArray())
		{
			try { await identityClient.EndWindowsSessionAsync(session.Sid, session.SessionId, cancellationToken); }
			catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException)
			{ logger.LogWarning("Agent session shutdown revoke failed; errorType={ErrorType}", ex.GetType().Name); }
		}
		await base.StopAsync(cancellationToken);
	}

	private static bool IsApprovedLocalExecutable(AgentLaunchDescriptor launch)
	{
		if (!Path.IsPathFullyQualified(launch.ExecutablePath) || !string.Equals(Path.GetExtension(launch.ExecutablePath), ".exe", StringComparison.OrdinalIgnoreCase) ||
			launch.ExecutableSha256.Length != 64 || !launch.ExecutableSha256.All(Uri.IsHexDigit) || launch.ExpiresAt <= DateTimeOffset.UtcNow) return false;
		try
		{
			var fullPath = Path.GetFullPath(launch.ExecutablePath);
			if (!string.Equals(fullPath, launch.ExecutablePath, StringComparison.OrdinalIgnoreCase) || !File.Exists(fullPath)) return false;
			var programRoots = new[] { Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86) }
				.Where(root => !string.IsNullOrWhiteSpace(root));
			if (!programRoots.Any(root => fullPath.StartsWith(Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))) return false;
			using var file = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
			var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(file));
			var signature = AuthenticodeVerifier.Verify(fullPath);
			return CryptographicOperations.FixedTimeEquals(Convert.FromHexString(hash), Convert.FromHexString(launch.ExecutableSha256)) &&
				signature.Trusted && ThumbprintsEqual(signature.PublisherThumbprint, launch.PublisherThumbprint);
		}
		catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException or FormatException or NotSupportedException) { return false; }
	}

	private static bool ThumbprintsEqual(string? left, string? right)
	{
		static byte[] Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? [] : value.Where(Uri.IsHexDigit).Select(char.ToUpperInvariant).Select(c => (byte)c).ToArray();
		var a = Normalize(left); var b = Normalize(right);
		return a.Length == 40 && b.Length == 40 && CryptographicOperations.FixedTimeEquals(a, b);
	}

	private static async Task WriteResponse(Stream pipe, Guid id, bool accepted, string code, CancellationToken ct,
		AgentLaunchDescriptor? launch = null)
	{
		var bytes = JsonSerializer.SerializeToUtf8Bytes(new AgentIpcResponse(IpcProtocol.CurrentVersion, id, accepted, code, launch));
        var header = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(header, bytes.Length);
        await pipe.WriteAsync(header, ct); await pipe.WriteAsync(bytes, ct); await pipe.FlushAsync(ct);
    }
}

internal static class WindowsCallerInspector
{
    public static WindowsCaller Inspect(NamedPipeServerStream pipe)
    {
        WindowsCaller? caller = null;
        pipe.RunAsClient(() =>
        {
            using var identity = WindowsIdentity.GetCurrent(TokenAccessLevels.Query);
            var sid = identity.User?.Value ?? throw new UnauthorizedAccessException("Caller SID unavailable.");
            var pipeIntegrity = GetIntegrityLevel(identity);
            if (!GetNamedPipeClientProcessId(pipe.SafePipeHandle, out var processId) || processId == 0)
                throw new UnauthorizedAccessException("Caller PID unavailable.");
            if (!ProcessIdToSessionId(processId, out var sessionId)) throw new UnauthorizedAccessException("Caller session unavailable.");
            if (!WindowsSessionInspector.IsConnectedAndUnlocked(checked((int)sessionId)))
				throw new UnauthorizedAccessException("Caller Windows session is not active and unlocked.");
            using var process = Process.GetProcessById(checked((int)processId));
            var processIdentity = GetProcessIdentity(process);
            if (!string.Equals(processIdentity.Sid, sid, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(processIdentity.Integrity, pipeIntegrity, StringComparison.Ordinal))
                throw new UnauthorizedAccessException("Pipe token does not match caller process security context.");
            var executablePath = Path.GetFullPath(process.MainModule?.FileName ?? throw new UnauthorizedAccessException("Caller image path unavailable."));
            var signature = AuthenticodeVerifier.Verify(executablePath);
			string? executableSha256 = null;
			try
			{
				using var image = new FileStream(executablePath, FileMode.Open, FileAccess.Read, FileShare.Read);
				executableSha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(image));
			}
			catch (IOException) { }
			caller = new WindowsCaller(sid, checked((int)sessionId), checked((int)processId), executablePath,
				pipeIntegrity, signature.Trusted, signature.PublisherThumbprint, executableSha256);
        });
        return caller ?? throw new UnauthorizedAccessException("Windows caller context unavailable.");
    }

    private static (string Sid, string Integrity) GetProcessIdentity(Process process)
    {
        using var token = OpenProcessTokenHandle(process);
        using var identity = new WindowsIdentity(token.DangerousGetHandle());
        return (identity.User?.Value ?? "", GetIntegrityLevel(identity));
    }

    private static string GetIntegrityLevel(WindowsIdentity identity)
    {
        var integritySid = identity.Groups?.FirstOrDefault(group => group.Value.StartsWith("S-1-16-", StringComparison.Ordinal))?.Value;
        var rid = integritySid?.Split('-').LastOrDefault();
        return rid switch { "4096" => "Low", "8192" => "Medium", "8448" => "MediumPlus", "12288" => "High", "16384" => "System", _ => "Unverified" };
    }

    private static SafeAccessTokenHandle OpenProcessTokenHandle(Process process)
    {
        if (!OpenProcessToken(process.Handle, 0x0008, out var handle)) throw new UnauthorizedAccessException("Caller process token unavailable.");
        return handle;
    }

    [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)] private static extern bool GetNamedPipeClientProcessId(Microsoft.Win32.SafeHandles.SafePipeHandle pipe, out uint clientProcessId);
    [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)] private static extern bool ProcessIdToSessionId(uint processId, out uint sessionId);
    [System.Runtime.InteropServices.DllImport("advapi32.dll", SetLastError = true)] private static extern bool OpenProcessToken(IntPtr processHandle, uint desiredAccess, out SafeAccessTokenHandle tokenHandle);
}

internal static class WindowsSessionInspector
{
	private const int WtsSessionInfoEx = 25;
	private const int WtsActive = 0;
	private const int WtsSessionStateUnlocked = 1;

	public static bool IsConnectedAndUnlocked(int sessionId)
	{
		if (!OperatingSystem.IsWindows() || sessionId < 0) return false;
		IntPtr buffer = IntPtr.Zero;
		try
		{
			// WTSINFOEXW contains a union aligned to pointer size (8 bytes on x64,
			// 4 bytes on x86); the Level1 payload starts after that aligned Level.
			var payloadOffset = IntPtr.Size == 8 ? 8 : 4;
			if (!WTSQuerySessionInformation(IntPtr.Zero, checked((uint)sessionId), WtsSessionInfoEx, out buffer, out var bytes) ||
				buffer == IntPtr.Zero || bytes < payloadOffset + 12 || bytes > 4096)
				return false;
			var data = new byte[checked((int)bytes)];
			Marshal.Copy(buffer, data, 0, data.Length);
			return IsSessionInfoActiveAndUnlocked(sessionId, data, IntPtr.Size);
		}
		catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or OverflowException) { return false; }
		finally { if (buffer != IntPtr.Zero) WTSFreeMemory(buffer); }
	}

	internal static bool IsSessionInfoActiveAndUnlocked(int expectedSessionId, ReadOnlySpan<byte> data, int pointerSize)
	{
		if (expectedSessionId < 0 || pointerSize is not (4 or 8)) return false;
		var payloadOffset = pointerSize == 8 ? 8 : 4;
		if (data.Length < payloadOffset + 12 || BinaryPrimitives.ReadInt32LittleEndian(data) != 1) return false;
		return BinaryPrimitives.ReadInt32LittleEndian(data.Slice(payloadOffset, 4)) == expectedSessionId &&
			BinaryPrimitives.ReadInt32LittleEndian(data.Slice(payloadOffset + 4, 4)) == WtsActive &&
			BinaryPrimitives.ReadInt32LittleEndian(data.Slice(payloadOffset + 8, 4)) == WtsSessionStateUnlocked;
	}

	[DllImport("wtsapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
	private static extern bool WTSQuerySessionInformation(IntPtr server, uint sessionId, int infoClass, out IntPtr buffer, out uint bytesReturned);
	[DllImport("wtsapi32.dll")] private static extern void WTSFreeMemory(IntPtr memory);
}

internal static class AuthenticodeVerifier
{
	private static readonly Guid GenericVerifyV2 = new("00AAC56B-CD44-11D0-8CC2-00C04FC295EE");
	private const uint UiNone = 2;
	private const uint UnionChoiceFile = 1;
	private const uint StateActionVerify = 1;
	private const uint StateActionClose = 2;
	private const uint RevocationCheckChain = 0x40;

	public static (bool Trusted, string? PublisherThumbprint) Verify(string path)
	{
		if (!OperatingSystem.IsWindows() || !File.Exists(path)) return (false, null);
		WinTrustFileInfo fileInfo = default;
		WinTrustData trustData = default;
		var actionId = GenericVerifyV2;
		IntPtr fileInfoPtr = IntPtr.Zero;
		IntPtr pathPtr = IntPtr.Zero;
		try
		{
			pathPtr = System.Runtime.InteropServices.Marshal.StringToCoTaskMemUni(path);
			fileInfo = new WinTrustFileInfo { Size = (uint)System.Runtime.InteropServices.Marshal.SizeOf<WinTrustFileInfo>(), FilePath = pathPtr };
			fileInfoPtr = System.Runtime.InteropServices.Marshal.AllocHGlobal(System.Runtime.InteropServices.Marshal.SizeOf<WinTrustFileInfo>());
			System.Runtime.InteropServices.Marshal.StructureToPtr(fileInfo, fileInfoPtr, false);
			trustData = new WinTrustData
			{
				Size = (uint)System.Runtime.InteropServices.Marshal.SizeOf<WinTrustData>(),
				UiChoice = UiNone, RevocationChecks = 1, UnionChoice = UnionChoiceFile, FileInfo = fileInfoPtr,
				StateAction = StateActionVerify, ProviderFlags = RevocationCheckChain
			};
			var status = WinVerifyTrust(IntPtr.Zero, ref actionId, ref trustData);
			if (status != 0) return (false, null);
			try
			{
				using var cert = new System.Security.Cryptography.X509Certificates.X509Certificate2(
					System.Security.Cryptography.X509Certificates.X509Certificate.CreateFromSignedFile(path));
				return (true, cert.Thumbprint?.Replace(" ", "", StringComparison.Ordinal).ToUpperInvariant());
			}
			catch (System.Security.Cryptography.CryptographicException) { return (false, null); }
		}
		catch (System.ComponentModel.Win32Exception) { return (false, null); }
		catch (System.Security.Cryptography.CryptographicException) { return (false, null); }
		finally
		{
			if (trustData.StateAction == StateActionVerify)
			{
				trustData.StateAction = StateActionClose;
				_ = WinVerifyTrust(IntPtr.Zero, ref actionId, ref trustData);
			}
			if (fileInfoPtr != IntPtr.Zero) System.Runtime.InteropServices.Marshal.FreeHGlobal(fileInfoPtr);
			if (pathPtr != IntPtr.Zero) System.Runtime.InteropServices.Marshal.FreeCoTaskMem(pathPtr);
		}
	}

	[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential, CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
	private struct WinTrustFileInfo
	{
		public uint Size;
		public IntPtr FilePath;
		public IntPtr FileHandle;
		public IntPtr KnownSubject;
	}

	[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential, CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
	private struct WinTrustData
	{
		public uint Size;
		public IntPtr PolicyCallbackData;
		public IntPtr SipClientData;
		public uint UiChoice;
		public uint RevocationChecks;
		public uint UnionChoice;
		public IntPtr FileInfo;
		public uint StateAction;
		public IntPtr StateData;
		public IntPtr UrlReference;
		public uint ProviderFlags;
		public uint UiContext;
		public IntPtr SignatureSettings;
	}

	[System.Runtime.InteropServices.DllImport("wintrust.dll", ExactSpelling = true, PreserveSig = true)]
	private static extern int WinVerifyTrust(IntPtr window, ref Guid actionId, ref WinTrustData trustData);
}
