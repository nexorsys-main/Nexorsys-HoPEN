using System;
using System.Threading.Tasks;

namespace Nexorsys.Identity.Core.Abstractions
{
	public interface IAnsDeclarationService
	{
		Task<bool> DeclareNfcAsTwoFactorAsync(string rppsNumber, string nfcUid, string verifiedByMethod);
		Task RecordDelegationAuditAsync(Guid userId, string rppsNumber, string nfcUid, string verifiedByMethod, Guid? adminUserId, bool declarationSuccess);
	}
}
