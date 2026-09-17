using System.Security.AccessControl;
using System.Security.Principal;
using System.Runtime.Versioning;

namespace Nexorsys.Identity.API.Services;

/// <summary>Fails startup when persisted Data Protection keys are missing, redirected, or broadly accessible.</summary>
public static class DataProtectionKeyDirectoryValidator
{
    private static readonly string[] WindowsBuiltInAllowedSids = ["S-1-5-18", "S-1-5-32-544"];
    private const UnixFileMode UnixUserOnly = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;
    private const UnixFileMode UnixGroupOrOther = UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute |
        UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute;
    private const UnixFileMode UnixKeyReadable = UnixFileMode.UserRead;

    public static void ValidateAndProbe(string directoryPath, IEnumerable<string>? additionalAllowedWindowsSids = null)
    {
        if (string.IsNullOrWhiteSpace(directoryPath))
            throw new InvalidOperationException("Data Protection key directory is required.");

        var fullPath = Path.GetFullPath(directoryPath);
        if (!Directory.Exists(fullPath))
            throw new InvalidOperationException("Data Protection key directory does not exist.");
        if ((File.GetAttributes(fullPath) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidOperationException("Data Protection key directory must not be a reparse point or symbolic link.");

        if (OperatingSystem.IsWindows())
            ValidateWindowsAccessControl(fullPath, additionalAllowedWindowsSids ?? []);
        else
            ValidateUnixPermissions(fullPath);

        ProbeReadWrite(fullPath);
    }

    [SupportedOSPlatform("windows")]
    private static void ValidateWindowsAccessControl(string path, IEnumerable<string> additionalAllowedSids)
    {
        var serviceSid = WindowsIdentity.GetCurrent().User
            ?? throw new InvalidOperationException("Unable to resolve the current Windows service identity SID.");
        var allowedSids = new HashSet<string>(WindowsBuiltInAllowedSids, StringComparer.OrdinalIgnoreCase)
        {
            serviceSid.Value
        };
        foreach (var configuredSid in additionalAllowedSids)
        {
            if (string.IsNullOrWhiteSpace(configuredSid))
                continue;
            try
            {
                allowedSids.Add(new SecurityIdentifier(configuredSid).Value);
            }
            catch (ArgumentException exception)
            {
                throw new InvalidOperationException("DataProtection:AllowedAccessSids contains an invalid Windows SID.", exception);
            }
        }

        ValidateWindowsRules(new DirectoryInfo(path).GetAccessControl(AccessControlSections.Access), path,
            serviceSid.Value, allowedSids, requireModify: true);

        foreach (var keyFile in Directory.EnumerateFiles(path, "*.xml", SearchOption.TopDirectoryOnly))
        {
            if ((File.GetAttributes(keyFile) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidOperationException("Data Protection key files must not be reparse points or symbolic links.");
            ValidateWindowsRules(new FileInfo(keyFile).GetAccessControl(), keyFile,
                serviceSid.Value, allowedSids, requireModify: false);
        }
    }

    [SupportedOSPlatform("windows")]
    private static void ValidateWindowsRules(FileSystemSecurity security, string path, string serviceSid,
        HashSet<string> allowedSids, bool requireModify)
    {
        var rules = security.GetAccessRules(includeExplicit: true, includeInherited: true, targetType: typeof(SecurityIdentifier));
        var serviceHasRequiredAccess = false;
        foreach (FileSystemAccessRule rule in rules)
        {
            if (rule.AccessControlType != AccessControlType.Allow)
                continue;

            var sid = ((SecurityIdentifier)rule.IdentityReference).Value;
            if (!allowedSids.Contains(sid))
                throw new InvalidOperationException($"Data Protection path '{Path.GetFileName(path)}' grants access to an unapproved Windows SID ({sid}).");

            var requiredRights = requireModify ? FileSystemRights.Modify : FileSystemRights.ReadData;
            if (string.Equals(sid, serviceSid, StringComparison.OrdinalIgnoreCase) &&
                (rule.FileSystemRights & requiredRights) == requiredRights)
                serviceHasRequiredAccess = true;
        }

        if (!serviceHasRequiredAccess)
        {
            var required = requireModify ? "Modify" : "Read";
            throw new InvalidOperationException($"Data Protection path '{Path.GetFileName(path)}' must grant {required} access directly to the current service identity.");
        }
    }

    [UnsupportedOSPlatform("windows")]
    private static void ValidateUnixPermissions(string path)
    {
        var mode = File.GetUnixFileMode(path);
        if ((mode & UnixGroupOrOther) != 0 || (mode & UnixUserOnly) != UnixUserOnly)
            throw new InvalidOperationException("On Unix hosts, the Data Protection key directory must grant owner read/write/execute only (mode 0700).");

        foreach (var keyFile in Directory.EnumerateFiles(path, "*.xml", SearchOption.TopDirectoryOnly))
        {
            if ((File.GetAttributes(keyFile) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidOperationException("Data Protection key files must not be reparse points or symbolic links.");
            var keyMode = File.GetUnixFileMode(keyFile);
            if ((keyMode & UnixGroupOrOther) != 0 || (keyMode & UnixKeyReadable) != UnixKeyReadable)
                throw new InvalidOperationException("On Unix hosts, Data Protection key files must be readable only by their owner.");
        }
    }

    private static void ProbeReadWrite(string path)
    {
        var probePath = Path.Combine(path, $".nexorsys-dp-probe-{Guid.NewGuid():N}");
        try
        {
            using var stream = new FileStream(probePath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None,
                bufferSize: 1, FileOptions.WriteThrough);
            stream.WriteByte(0x5A);
            stream.Flush(flushToDisk: true);
            stream.Position = 0;
            if (stream.ReadByte() != 0x5A)
                throw new InvalidOperationException("Data Protection key directory read/write verification failed.");
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or IOException)
        {
            throw new InvalidOperationException("The current API service identity cannot safely read and write the Data Protection key directory.", exception);
        }
        finally
        {
            try
            {
                if (File.Exists(probePath)) File.Delete(probePath);
            }
            catch (Exception exception) when (exception is UnauthorizedAccessException or IOException)
            {
                throw new InvalidOperationException("Unable to remove the temporary Data Protection access probe.", exception);
            }
        }
    }
}
