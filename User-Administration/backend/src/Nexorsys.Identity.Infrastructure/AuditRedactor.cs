using System.Text.RegularExpressions;

namespace Nexorsys.Identity.Infrastructure;

public static partial class AuditRedactor
{
    [GeneratedRegex("(?i)(?<key>\\b(?:password|passwd|pin|badge(?:uid)?|nfcbadgeuid|cpsid|fidoid|(?:access|refresh|session)?token|secret|credential|api[_-]?key|private[_ -]?key)\\b\\s*[:=]\\s*)(?<value>[^,;\\r\\n}]+)", RegexOptions.CultureInvariant)]
    private static partial Regex SensitiveKeyValue();

    [GeneratedRegex("(?i)(?<key>\\\"(?:password|passwd|pin|badge(?:uid)?|nfcbadgeuid|cpsid|fidoid|(?:access|refresh|session)?token|secret|credential|api[_-]?key|private[_ -]?key)\\\"\\s*:\\s*\\\")(?<value>(?:\\\\.|[^\\\"\\\\])*)(?<suffix>\\\")", RegexOptions.CultureInvariant)]
    private static partial Regex SensitiveJsonValue();

    public static string? Redact(string? value) => value is null ? null : SensitiveKeyValue().Replace(
        SensitiveJsonValue().Replace(value, "${key}[REDACTED]${suffix}"), "${key}[REDACTED]");
}
