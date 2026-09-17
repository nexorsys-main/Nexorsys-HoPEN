using Nexorsys.WindowsAgent;
using Xunit;

namespace Nexorsys.WindowsAgent.Tests;

public sealed class WindowsIdentityHostTests
{
	[Fact]
	public void Wts_session_state_parser_accepts_only_the_requested_active_unlocked_session()
	{
		const int sessionId = 17;
		var unlocked = new byte[20];
		System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(unlocked.AsSpan(0, 4), 1);
		System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(unlocked.AsSpan(8, 4), sessionId);
		System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(unlocked.AsSpan(12, 4), 0);
		System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(unlocked.AsSpan(16, 4), 1);
		Assert.True(WindowsSessionInspector.IsSessionInfoActiveAndUnlocked(sessionId, unlocked, 8));
		Assert.False(WindowsSessionInspector.IsSessionInfoActiveAndUnlocked(sessionId + 1, unlocked, 8));
		Assert.False(WindowsSessionInspector.IsSessionInfoActiveAndUnlocked(-1, unlocked, 8));
		Assert.False(WindowsSessionInspector.IsSessionInfoActiveAndUnlocked(sessionId, unlocked.AsSpan(0, 19), 8));

		var locked = (byte[])unlocked.Clone();
		System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(locked.AsSpan(16, 4), 2);
		Assert.False(WindowsSessionInspector.IsSessionInfoActiveAndUnlocked(sessionId, locked, 8));

		var disconnected = (byte[])unlocked.Clone();
		System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(disconnected.AsSpan(12, 4), 4);
		Assert.False(WindowsSessionInspector.IsSessionInfoActiveAndUnlocked(sessionId, disconnected, 8));
	}

	[Fact]
	public void Wintrust_accepts_the_current_dotnet_signed_executable_and_returns_its_publisher()
	{
		var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
		var signedDotnet = Path.Combine(programFiles, "dotnet", "dotnet.exe");
		Assert.True(File.Exists(signedDotnet), "Windows-host test requires the signed .NET host in Program Files.");
		var result = AuthenticodeVerifier.Verify(signedDotnet);
		Assert.True(result.Trusted, "WinVerifyTrust did not trust the installed signed .NET executable.");
		Assert.Matches("^[A-F0-9]{40}$", result.PublisherThumbprint ?? string.Empty);
	}

	[Fact]
	public void Unsigned_test_assembly_is_rejected_by_authenticode()
	{
		var result = AuthenticodeVerifier.Verify(typeof(WindowsIdentityHostTests).Assembly.Location);
		Assert.False(result.Trusted);
		Assert.Null(result.PublisherThumbprint);
	}
}
