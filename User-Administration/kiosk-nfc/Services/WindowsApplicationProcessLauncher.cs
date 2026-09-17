using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace Nexorsys.NFC.APP.Services;

public sealed class WindowsApplicationProcessLauncher : IApplicationProcessLauncher
{
    public bool TryStart(string verifiedExecutablePath, Func<Task> onExit)
    {
        var process = Process.Start(new ProcessStartInfo
        {
            FileName = verifiedExecutablePath,
            WorkingDirectory = Path.GetDirectoryName(verifiedExecutablePath)!,
            UseShellExecute = false,
            CreateNoWindow = false
        });
        if (process is null) return false;
        _ = ObserveExitAsync(process, onExit);
        return true;
    }

    private static async Task ObserveExitAsync(Process process, Func<Task> onExit)
    {
        using (process)
        {
            try { await process.WaitForExitAsync(); }
            catch (InvalidOperationException) { }
            await onExit();
        }
    }
}
