using Nexorsys.Identity.Core;

namespace Nexorsys.Identity.Core.Abstractions
{
	public interface IAuditService
	{
		void Add(string action, string? resourceType = null, Guid? resourceId = null, string? oldValues = null, string? newValues = null, Guid? userId = null);
		Task LogAsync(string action, string? resourceType = null, Guid? resourceId = null, string? oldValues = null, string? newValues = null, Guid? userId = null);
		Task LogUserActionAsync(Guid userId, string action, string? resourceType = null, Guid? resourceId = null, string? oldValues = null, string? newValues = null);
	}
}
