using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;
using Nexorsys.Identity.API.Services;

namespace Nexorsys.Identity.API.Hubs;

/// <summary>
/// Authenticated hub boundary. Legacy unauthenticated bridge commands were removed;
/// hardware telemetry and credential pushes require a future certificate-bound protocol.
/// </summary>
[Authorize]
public sealed class IdentityHub : Hub
{
	private readonly ILicenseEntitlementGuard _licenseGuard;

	public IdentityHub(ILicenseEntitlementGuard licenseGuard) => _licenseGuard = licenseGuard;

	public static string AdminGroupName(Guid organizationId) => $"organization:{organizationId:N}:admins";

	public static string? AdminGroupFor(ClaimsPrincipal? user)
	{
		if (user?.IsInRole("Admin") != true && user?.IsInRole("ADMIN") != true &&
			user?.IsInRole("ADMIN_DSI") != true && user?.IsInRole("SUPERADMIN") != true)
			return null;

		return Guid.TryParse(user.FindFirst("org_id")?.Value, out var organizationId)
			? AdminGroupName(organizationId)
			: null;
	}

	public override async Task OnConnectedAsync()
	{
		var adminGroup = await LicensedAdminGroupForAsync(Context.User, _licenseGuard);
		if (adminGroup is not null)
			await Groups.AddToGroupAsync(Context.ConnectionId, adminGroup);
		await base.OnConnectedAsync();
	}

	public async Task JoinEnrollmentRoom()
	{
		RequireAdministrator();
		var adminGroup = await LicensedAdminGroupForAsync(Context.User, _licenseGuard);
		if (adminGroup is null) throw new HubException("A tenant-bound administrator identity is required.");
		await Groups.AddToGroupAsync(Context.ConnectionId, adminGroup);
	}

	internal static async Task<string?> LicensedAdminGroupForAsync(ClaimsPrincipal? user, ILicenseEntitlementGuard licenseGuard)
	{
		var group = AdminGroupFor(user);
		if (group is null || !Guid.TryParse(user?.FindFirst("org_id")?.Value, out var organizationId)) return null;
		var entitlement = await licenseGuard.HasFeatureAsync(organizationId, "identity");
		return entitlement.Allowed ? group : null;
	}

	public Task NotifyHardwareDetected(string identifier, string hardwareType, string machineName) =>
		throw new HubException("Authenticated workstation hardware telemetry is not configured.");

	public Task NotifyReaderStatus(string machineName, bool isConnected) =>
		throw new HubException("Authenticated workstation hardware telemetry is not configured.");

	public Task RequestHardwareScan()
	{
		RequireAdministrator();
		throw new HubException("Authenticated kiosk command transport is not configured.");
	}

	public Task UpdateAdminPassword(string newPassword)
	{
		RequireAdministrator();
		throw new HubException("Password broadcast is disabled. Configure a secure credential-management channel.");
	}

	public Task SendWriteCuidCommand(string targetMachine, string newUid)
	{
		RequireAdministrator();
		throw new HubException("Authenticated NFC write transport is not configured.");
	}

	private void RequireAdministrator()
	{
		var user = Context.User;
		if (user?.IsInRole("Admin") != true && user?.IsInRole("ADMIN") != true &&
			user?.IsInRole("ADMIN_DSI") != true && user?.IsInRole("SUPERADMIN") != true)
			throw new HubException("Administrator role required.");
	}
}
