using System.Diagnostics;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Nexorsys.Agent.Core;
using Nexorsys.NFC.APP.Modeles;
using Nexorsys.NFC.APP.Services;
using Xunit;

namespace Nexorsys.Kiosk.Tests;

public sealed class KioskLaunchTests
{
    [Fact]
    public void Grant_policy_rejects_bad_path_session_hash_and_expiration()
    {
        var now = DateTimeOffset.UtcNow;
        var session = Guid.NewGuid();
        var baseline = new AgentLaunchDescriptor(session, Guid.NewGuid(), @"C:\Program Files\Vendor\app.exe",
            new string('A', 40), new string('B', 64), now.AddSeconds(10));
        Assert.False(ExecutableLaunchGrantValidator.IsValid(baseline, session, now));
        Assert.False(ExecutableLaunchGrantValidator.IsValid(baseline with { ExecutablePath = "https://attacker.invalid/app.exe" }, session, now));
        Assert.False(ExecutableLaunchGrantValidator.IsValid(baseline with { ExecutablePath = @"C:\Program Files\Vendor\..\Temp\app.exe" }, session, now));
        Assert.False(ExecutableLaunchGrantValidator.IsValid(baseline with { ApplicationSessionId = Guid.NewGuid() }, session, now));
        Assert.False(ExecutableLaunchGrantValidator.IsValid(baseline with { ExecutableSha256 = new string('C', 64) }, session, now));
        Assert.False(ExecutableLaunchGrantValidator.IsValid(baseline with { ExpiresAt = now.AddSeconds(-1) }, session, now));
    }

    [Fact]
    public async Task Kiosk_launch_fails_closed_when_agent_is_unavailable_and_stops_server_session()
    {
        var sessions = new FakeSessions();
        var process = new FakeProcessLauncher();
        var logger = new FakeJournal();
        var service = new LanceurApplicationService(logger, sessions, new FakeAgent(null), process);

        var launched = await service.LancerApplicationAsync(new ManagedApp { Id = "app-client-id", Path = "https://attacker.invalid" });

        Assert.False(launched);
        Assert.Equal(1, sessions.StopCount);
        Assert.Equal(0, process.StartCount);
        Assert.Contains(logger.Errors, message => message.Contains("Agent denied", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("expired")]
    [InlineData("wrong-session")]
    [InlineData("malicious-url")]
    [InlineData("untrusted-path")]
    [InlineData("bad-publisher")]
    [InlineData("bad-hash")]
    public async Task Kiosk_never_starts_process_for_invalid_or_untrusted_agent_grant(string scenario)
    {
        var sessionId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var grant = new AgentLaunchDescriptor(sessionId, Guid.NewGuid(),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "NexorSys", "app.exe"),
            new string('A', 40), new string('B', 64), now.AddSeconds(10));
        grant = scenario switch
        {
            "expired" => grant with { ExpiresAt = now.AddSeconds(-1) },
            "wrong-session" => grant with { ApplicationSessionId = Guid.NewGuid() },
            "malicious-url" => grant with { ExecutablePath = "https://attacker.invalid/launch.exe" },
            "untrusted-path" => grant with { ExecutablePath = @"C:\Temp\app.exe" },
            "bad-publisher" => grant with { PublisherThumbprint = "not-a-thumbprint" },
            "bad-hash" => grant with { ExecutableSha256 = "not-a-hash" },
            _ => throw new ArgumentOutOfRangeException(nameof(scenario))
        };
        var sessions = new FakeSessions(sessionId);
        var process = new FakeProcessLauncher();
        var service = new LanceurApplicationService(new FakeJournal(), sessions, new FakeAgent(grant), process);

        var launched = await service.LancerApplicationAsync(new ManagedApp { Id = "registered-app", Path = "C:\\attacker.exe" });

        Assert.False(launched);
        Assert.Equal(0, process.StartCount);
        Assert.Equal(1, sessions.StopCount);
    }

    [Fact]
    public async Task Kiosk_does_not_contact_agent_when_server_did_not_create_session()
    {
        var sessions = new FakeSessions(noSession: true);
        var agent = new FakeAgent(null);
        var process = new FakeProcessLauncher();
        var service = new LanceurApplicationService(new FakeJournal(), sessions, agent, process);

        var launched = await service.LancerApplicationAsync(new ManagedApp { Id = "registered-app" });

        Assert.False(launched);
        Assert.Equal(0, agent.CallCount);
        Assert.Equal(0, sessions.StopCount);
        Assert.Equal(0, process.StartCount);
    }

    [Fact]
    public async Task Kiosk_cleans_up_server_session_when_process_launcher_rejects_start()
    {
        var executable = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "dotnet", "dotnet.exe");
        Assert.True(File.Exists(executable), "Controlled launch regression requires the signed .NET host in Program Files.");
        using var certificate = new X509Certificate2(X509Certificate.CreateFromSignedFile(executable));
        using var stream = new FileStream(executable, FileMode.Open, FileAccess.Read, FileShare.Read);
        var sessionId = Guid.NewGuid();
        var sessions = new FakeSessions(sessionId);
        var process = new FakeProcessLauncher { StartResult = false };
        var service = new LanceurApplicationService(new FakeJournal(), sessions,
            new FakeAgent(new AgentLaunchDescriptor(sessionId, Guid.NewGuid(), executable,
                certificate.Thumbprint!, Convert.ToHexString(SHA256.HashData(stream)), DateTimeOffset.UtcNow.AddSeconds(10))), process);

        var launched = await service.LancerApplicationAsync(new ManagedApp { Id = "registered-app" });

        Assert.False(launched);
        Assert.Equal(1, process.StartCount);
        Assert.Equal(1, sessions.StopCount);
    }

    [Fact]
    public async Task Signed_controlled_executable_launches_only_after_agent_grant_and_without_arguments()
    {
        var executable = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "dotnet", "dotnet.exe");
        Assert.True(File.Exists(executable), "Controlled launch regression requires the signed .NET host in Program Files.");
        var session = Guid.NewGuid();
        using var certificate = new X509Certificate2(X509Certificate.CreateFromSignedFile(executable));
        using var stream = new FileStream(executable, FileMode.Open, FileAccess.Read, FileShare.Read);
        var descriptor = new AgentLaunchDescriptor(session, Guid.NewGuid(), executable, certificate.Thumbprint!,
            Convert.ToHexString(SHA256.HashData(stream)), DateTimeOffset.UtcNow.AddSeconds(15));
        var sessions = new FakeSessions(session);
        var processLauncher = new WindowsApplicationProcessLauncher();
        var exited = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var observingProcess = new ExitObservingLauncher(processLauncher, exited);
        var service = new LanceurApplicationService(new FakeJournal(), sessions, new FakeAgent(descriptor), observingProcess);

        var launched = await service.LancerApplicationAsync(new ManagedApp
        {
            Id = "registered-app", Path = "file:///C:/attacker.exe", Description = "client path must be ignored"
        });

        Assert.True(launched);
        await exited.Task.WaitAsync(TimeSpan.FromSeconds(15));
        Assert.Equal(1, sessions.StopCount);
        Assert.Equal(executable, observingProcess.StartedPath);
    }

    private sealed class FakeSessions(Guid? sessionId = null, bool noSession = false) : IKioskApplicationSessionClient
    {
        private readonly Guid? _sessionId = noSession ? null : sessionId ?? Guid.NewGuid();
        public int StopCount { get; private set; }
        public Task<Guid?> StartApplicationSessionAsync(string applicationId) => Task.FromResult(_sessionId);
        public Task StopApplicationSessionAsync(Guid applicationSessionId) { StopCount++; return Task.CompletedTask; }
    }

    private sealed class FakeAgent(AgentLaunchDescriptor? grant) : IAgentLaunchAuthorizationClient
    {
        public int CallCount { get; private set; }
        public Task<AgentLaunchDescriptor?> AuthorizeAsync(Guid applicationSessionId, CancellationToken cancellationToken = default) =>
            ReturnGrant(applicationSessionId);

        private Task<AgentLaunchDescriptor?> ReturnGrant(Guid applicationSessionId)
        {
            CallCount++;
            return Task.FromResult(grant is { } value && value.ApplicationSessionId != applicationSessionId
                ? (AgentLaunchDescriptor?)null : grant);
        }
    }

    private sealed class FakeProcessLauncher : IApplicationProcessLauncher
    {
        public int StartCount { get; private set; }
        public bool StartResult { get; init; } = true;
        public bool TryStart(string verifiedExecutablePath, Func<Task> onExit) { StartCount++; return StartResult; }
    }

    private sealed class ExitObservingLauncher(IApplicationProcessLauncher inner, TaskCompletionSource exited) : IApplicationProcessLauncher
    {
        public string? StartedPath { get; private set; }
        public bool TryStart(string verifiedExecutablePath, Func<Task> onExit)
        {
            StartedPath = verifiedExecutablePath;
            return inner.TryStart(verifiedExecutablePath, async () => { await onExit(); exited.TrySetResult(); });
        }
    }

    private sealed class FakeJournal : IJournalisationService
    {
        public List<string> Errors { get; } = [];
        public void EnregistrerInformation(string message, string? uidBadge = null, string? utilisateur = null) { }
        public void EnregistrerErreur(string message, string? uidBadge = null, string? utilisateur = null) => Errors.Add(message);
        public void EnregistrerEvenement(EvenementJournal evenement) { }
        public List<EvenementJournal> ObtenirHistorique() => [];
    }
}
