using Nexorsys.Agent.Core;
using Xunit;

namespace Nexorsys.WindowsAgent.Tests;

public sealed class ExecutableImageIdentityPolicyTests
{
    private const string Publisher = "0123456789ABCDEF0123456789ABCDEF01234567";
    private const string Hash = "0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF";

    [Fact]
    public void Exact_path_publisher_hash_and_trusted_signature_are_all_required()
    {
        var path = Path.Combine(Path.GetTempPath(), "NexorSys", "Kiosk.exe");
        Assert.True(ExecutableImageIdentityPolicy.IsAllowed(path, Publisher, Hash, path.ToLowerInvariant(), Publisher.ToLowerInvariant(), Hash.ToLowerInvariant(), true));
        Assert.False(ExecutableImageIdentityPolicy.IsAllowed(path, Publisher, Hash, path + ".other", Publisher, Hash, true));
        Assert.False(ExecutableImageIdentityPolicy.IsAllowed(path, Publisher, Hash, path, new string('A', 40), Hash, true));
        Assert.False(ExecutableImageIdentityPolicy.IsAllowed(path, Publisher, Hash, path, Publisher, new string('A', 64), true));
        Assert.False(ExecutableImageIdentityPolicy.IsAllowed(path, Publisher, Hash, path, Publisher, Hash, false));
        Assert.False(ExecutableImageIdentityPolicy.IsAllowed(path, Publisher, Hash, path, Publisher, "invalid", true));
    }
}
