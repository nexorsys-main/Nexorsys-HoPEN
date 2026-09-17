using System;
using System.Threading;
using System.Threading.Tasks;
using Nexorsys.Agent.Core;

namespace Nexorsys.NFC.APP.Services;

public interface IAgentLaunchAuthorizationClient
{
    Task<AgentLaunchDescriptor?> AuthorizeAsync(Guid applicationSessionId, CancellationToken cancellationToken = default);
}
