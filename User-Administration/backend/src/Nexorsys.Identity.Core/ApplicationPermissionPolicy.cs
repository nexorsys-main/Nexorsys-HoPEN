namespace Nexorsys.Identity.Core;

/// <summary>Only explicit use/launch grants authorize starting an application or releasing its credentials.</summary>
public static class ApplicationPermissionPolicy
{
	public const string Use = "use";
	public const string Launch = "launch";

	public static bool AllowsLaunch(string? permissionLevel) =>
		string.Equals(permissionLevel?.Trim(), Use, StringComparison.OrdinalIgnoreCase) ||
		string.Equals(permissionLevel?.Trim(), Launch, StringComparison.OrdinalIgnoreCase);
}
