using System;
using System.Threading.Tasks;

namespace Nexorsys.NFC.APP.Services;

public interface IApplicationProcessLauncher
{
    bool TryStart(string verifiedExecutablePath, Func<Task> onExit);
}
