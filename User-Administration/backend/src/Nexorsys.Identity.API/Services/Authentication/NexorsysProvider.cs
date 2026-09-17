using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using Nexorsys.Identity.Core.Abstractions;
using Nexorsys.Identity.Infrastructure;

namespace Nexorsys.Identity.API.Services.Authentication
{
	public class NexorsysProvider : IAuthenticationProvider
	{
		private readonly AppDbContext _dbContext;
		private readonly IOrganizationContext _organization;

		public NexorsysProvider(AppDbContext dbContext, IOrganizationContext organization)
		{
			_dbContext = dbContext;
			_organization = organization;
		}

		public string ProviderType => "NFC";
		public int Priority => 1; // Highest priority

		public async Task<AuthResult> AuthenticateAsync(AuthContext context)
		{
			if (string.IsNullOrEmpty(context.Pin) || !_organization.OrganizationId.HasValue || _organization.OrganizationId.Value == Guid.Empty)
				return new AuthResult { IsSuccess = false, ErrorMessage = "PIN is required." };
			var organizationId = _organization.OrganizationId.Value;

			var user = await _dbContext.Users
				.FirstOrDefaultAsync(u => u.OrganizationId == organizationId && u.BadgeUid == context.BadgeUid && u.IsActive &&
					(!u.LockedUntil.HasValue || u.LockedUntil <= DateTime.UtcNow));
			if (user == null)
				return new AuthResult { IsSuccess = false, ErrorMessage = "User not found for this badge." };

			var activePin = await _dbContext.UserPins.FirstOrDefaultAsync(p =>
				p.OrganizationId == organizationId && p.UserId == user.Id && p.IsActive);
			if (activePin == null || string.IsNullOrWhiteSpace(activePin.PinHash) ||
				!BCrypt.Net.BCrypt.Verify(context.Pin, activePin.PinHash))
				return new AuthResult { IsSuccess = false, ErrorMessage = "Invalid PIN." };

			return new AuthResult
			{
				IsSuccess = true,
				Profile = new UserProfile
				{
					SubjectId = user.Id.ToString(),
					Username = user.SamAccountName,
					DisplayName = user.DisplayName
				}
			};
		}

		public Task<bool> ValidateAsync(ValidationContext context)
		{
			// A provider-local boolean is not a substitute for server session/token validation.
			return Task.FromResult(false);
		}

		public async Task<UserProfile> GetProfileAsync(string subjectId)
		{
			if (Guid.TryParse(subjectId, out var id))
			{
				if (!_organization.OrganizationId.HasValue || _organization.OrganizationId.Value == Guid.Empty)
					throw new KeyNotFoundException("The requested identity profile was not found.");
				var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == id && u.OrganizationId == _organization.OrganizationId.Value);
				if (user != null)
				{
					return new UserProfile
					{
						SubjectId = user.Id.ToString(),
						Username = user.SamAccountName,
						DisplayName = user.DisplayName
					};
				}
			}
			throw new KeyNotFoundException("The requested identity profile was not found.");
		}
	}
}
