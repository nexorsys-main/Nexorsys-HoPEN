using System;
using System.Text;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Nexorsys.Identity.Core.Abstractions;
using Nexorsys.Identity.Core;

namespace Nexorsys.Identity.Infrastructure
{
	public class AnsDeclarationService : IAnsDeclarationService
	{
		private readonly ILogger<AnsDeclarationService> _logger;
		private readonly AppDbContext _context;
		private readonly IOrganizationContext _organization;

		public AnsDeclarationService(ILogger<AnsDeclarationService> logger, AppDbContext context, IOrganizationContext organization)
		{
			_logger = logger;
			_context = context;
			_organization = organization;
		}

		public Task<bool> DeclareNfcAsTwoFactorAsync(string rppsNumber, string nfcUid, string verifiedByMethod)
		{
			_ = rppsNumber; _ = nfcUid; _ = verifiedByMethod;
			_logger.LogWarning("ANS declaration is not configured; no external declaration was made.");
			return Task.FromResult(false);
		}

		public async Task RecordDelegationAuditAsync(Guid userId, string rppsNumber, string nfcUid, string verifiedByMethod, Guid? adminUserId, bool declarationSuccess)
		{
			if (!_organization.OrganizationId.HasValue || _organization.OrganizationId.Value == Guid.Empty)
				throw new InvalidOperationException("Audit organization context is unavailable.");
			var organizationId = _organization.OrganizationId.Value;
			var userBelongsToOrganization = await _context.Users.IgnoreQueryFilters()
				.AnyAsync(u => u.Id == userId && u.OrganizationId == organizationId);
			if (!userBelongsToOrganization)
				throw new InvalidOperationException("Audit owner is unavailable in the current organization.");
			if (adminUserId.HasValue && (!await _context.Users.IgnoreQueryFilters()
				.AnyAsync(u => u.Id == adminUserId.Value && u.OrganizationId == organizationId)))
				throw new InvalidOperationException("Audit actor is unavailable in the current organization.");
			var log = new AnsDelegationLog
			{
				Id = Guid.NewGuid(),
				OrganizationId = organizationId,
				UserId = userId,
				RppsNumber = rppsNumber,
				NfcBadgeUid = string.Empty,
				CredentialDigestSha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(nfcUid))),
				VerificationMethod = verifiedByMethod,
				PairedAt = DateTime.UtcNow,
				AdminUserId = adminUserId,
				IsDeclaredToGovernment = declarationSuccess
			};

			_context.AnsDelegationLogs.Add(log);
			await _context.SaveChangesAsync();
		}
	}
}
