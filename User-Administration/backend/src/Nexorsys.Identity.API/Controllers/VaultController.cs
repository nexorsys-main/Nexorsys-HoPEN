using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nexorsys.Identity.Core;
using Nexorsys.Identity.Core.Abstractions;
using Nexorsys.Identity.API.Services;

namespace Nexorsys.Identity.API.Controllers;

[ApiController]
[Authorize]
[Route("api/vault")]
public sealed class VaultController : ControllerBase
{
	private readonly IVaultService _vault;
	private readonly ILicenseEntitlementGuard _licenseGuard;
	private readonly IAuthorizationService _authorizationService;

	public VaultController(IVaultService vault, ILicenseEntitlementGuard licenseGuard, IAuthorizationService authorizationService)
	{
		_vault = vault;
		_licenseGuard = licenseGuard;
		_authorizationService = authorizationService;
	}

	[HttpPost("{entryId:guid}/authorize-use")]
	public async Task<IActionResult> AuthorizeUse(Guid entryId, [FromBody] VaultUseRequest request, CancellationToken cancellationToken)
	{
		var userId = GetUserId();
		var organizationId = GetOrganizationId();
		if (userId == Guid.Empty || organizationId == Guid.Empty) return Forbid();
		var entitlement = await _licenseGuard.HasFeatureAsync(organizationId, "identity", cancellationToken);
		if (!entitlement.Allowed)
			return new ObjectResult(new { code = entitlement.Code }) { StatusCode = StatusCodes.Status403Forbidden };
		var allowed = await _vault.CanUseAsync(organizationId, entryId, userId, request.WorkstationId, request.SessionId, cancellationToken);
		if (!allowed) return Forbid();

		// Deliberately return an authorization decision only. Plaintext credentials
		// are released by a future authenticated Windows agent channel, never REST.
		return Ok(new { authorized = true, entryId, expiresInSeconds = 30 });
	}

	[HttpGet]
	public async Task<IActionResult> List(CancellationToken cancellationToken)
	{
		var organizationId = GetOrganizationId();
		if (organizationId == Guid.Empty) return Forbid();
		var entitlement = await _licenseGuard.HasFeatureAsync(organizationId, "identity", cancellationToken);
		if (!entitlement.Allowed) return new ObjectResult(new { code = entitlement.Code }) { StatusCode = StatusCodes.Status403Forbidden };
		
		// Typically an admin policy check would go here: e.g. await _authorizationService.AuthorizeAsync(User, "Vault.ReadMetadata")
		// We rely on the tenant isolation in the service and assume the UI only exposes this to authorized users. 
		// But let's check a generic admin role for now.
		if (!User.IsInRole("Administrator") && !User.IsInRole("VaultAdministrator")) return Forbid();

		var entries = await _vault.ListAsync(organizationId, cancellationToken);
		return Ok(entries.Select(MapToDto));
	}

	[HttpGet("{entryId:guid}")]
	public async Task<IActionResult> Get(Guid entryId, CancellationToken cancellationToken)
	{
		var organizationId = GetOrganizationId();
		if (organizationId == Guid.Empty) return Forbid();
		var entitlement = await _licenseGuard.HasFeatureAsync(organizationId, "identity", cancellationToken);
		if (!entitlement.Allowed) return new ObjectResult(new { code = entitlement.Code }) { StatusCode = StatusCodes.Status403Forbidden };
		if (!User.IsInRole("Administrator") && !User.IsInRole("VaultAdministrator")) return Forbid();

		var entry = await _vault.GetAsync(organizationId, entryId, cancellationToken);
		if (entry == null) return NotFound();
		return Ok(MapToDto(entry));
	}

	[HttpPost]
	public async Task<IActionResult> Create([FromBody] VaultCreateRequest request, CancellationToken cancellationToken)
	{
		var userId = GetUserId();
		var organizationId = GetOrganizationId();
		if (userId == Guid.Empty || organizationId == Guid.Empty) return Forbid();
		var entitlement = await _licenseGuard.HasFeatureAsync(organizationId, "identity", cancellationToken);
		if (!entitlement.Allowed) return new ObjectResult(new { code = entitlement.Code }) { StatusCode = StatusCodes.Status403Forbidden };
		if (!User.IsInRole("Administrator") && !User.IsInRole("VaultAdministrator")) return Forbid();

		var entry = await _vault.StoreAsync(organizationId, request.ApplicationId, request.SiteId, request.Name, request.CredentialType, request.UserId, request.Username, request.Secret, userId, cancellationToken);
		return Ok(MapToDto(entry));
	}

	[HttpPost("{entryId:guid}/rotate")]
	public async Task<IActionResult> Rotate(Guid entryId, [FromBody] VaultRotateRequest request, CancellationToken cancellationToken)
	{
		var userId = GetUserId();
		var organizationId = GetOrganizationId();
		if (userId == Guid.Empty || organizationId == Guid.Empty) return Forbid();
		var entitlement = await _licenseGuard.HasFeatureAsync(organizationId, "identity", cancellationToken);
		if (!entitlement.Allowed) return new ObjectResult(new { code = entitlement.Code }) { StatusCode = StatusCodes.Status403Forbidden };
		if (!User.IsInRole("Administrator") && !User.IsInRole("VaultAdministrator")) return Forbid();

		try
		{
			var entry = await _vault.RotateAsync(organizationId, entryId, request.NewSecret, userId, cancellationToken);
			return Ok(MapToDto(entry));
		}
		catch (InvalidOperationException)
		{
			return BadRequest();
		}
	}

	[HttpPost("{entryId:guid}/revoke")]
	public async Task<IActionResult> Revoke(Guid entryId, CancellationToken cancellationToken)
	{
		var userId = GetUserId();
		var organizationId = GetOrganizationId();
		if (userId == Guid.Empty || organizationId == Guid.Empty) return Forbid();
		var entitlement = await _licenseGuard.HasFeatureAsync(organizationId, "identity", cancellationToken);
		if (!entitlement.Allowed) return new ObjectResult(new { code = entitlement.Code }) { StatusCode = StatusCodes.Status403Forbidden };
		if (!User.IsInRole("Administrator") && !User.IsInRole("VaultAdministrator")) return Forbid();

		try
		{
			var entry = await _vault.RevokeAsync(organizationId, entryId, userId, cancellationToken);
			return Ok(MapToDto(entry));
		}
		catch (InvalidOperationException)
		{
			return BadRequest();
		}
	}

	[HttpDelete("{entryId:guid}")]
	public async Task<IActionResult> Delete(Guid entryId, CancellationToken cancellationToken)
	{
		var userId = GetUserId();
		var organizationId = GetOrganizationId();
		if (userId == Guid.Empty || organizationId == Guid.Empty) return Forbid();
		var entitlement = await _licenseGuard.HasFeatureAsync(organizationId, "identity", cancellationToken);
		if (!entitlement.Allowed) return new ObjectResult(new { code = entitlement.Code }) { StatusCode = StatusCodes.Status403Forbidden };
		if (!User.IsInRole("Administrator") && !User.IsInRole("VaultAdministrator")) return Forbid();

		await _vault.DeleteAsync(organizationId, entryId, userId, cancellationToken);
		return NoContent();
	}

	private Guid GetUserId() => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : Guid.Empty;
	private Guid GetOrganizationId() => Guid.TryParse(User.FindFirstValue("org_id"), out var id) ? id : Guid.Empty;

	private static object MapToDto(VaultEntry entry)
	{
		return new {
			id = entry.Id,
			applicationId = entry.ApplicationId,
			siteId = entry.SiteId,
			name = entry.Name,
			credentialType = entry.CredentialType,
			userId = entry.UserId,
			username = entry.Username,
			status = entry.Status,
			createdAt = entry.CreatedAt,
			updatedAt = entry.UpdatedAt,
			lastRotatedAt = entry.LastRotatedAt,
			nextRotationAt = entry.NextRotationAt,
			revokedAt = entry.RevokedAt,
			expiresAt = entry.ExpiresAt,
			secretVersion = entry.SecretVersion,
			version = entry.Version
		};
	}

	public sealed record VaultUseRequest(Guid WorkstationId, Guid SessionId);
	public sealed record VaultCreateRequest(Guid ApplicationId, Guid? SiteId, string Name, string CredentialType, Guid? UserId, string Username, string Secret);
	public sealed record VaultRotateRequest(string NewSecret);
}
