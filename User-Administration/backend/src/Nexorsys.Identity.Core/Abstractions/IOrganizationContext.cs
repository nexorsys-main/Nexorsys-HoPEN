namespace Nexorsys.Identity.Core.Abstractions;

public interface IOrganizationContext
{
	Guid? OrganizationId { get; }
	bool IsSystemAdministrator { get; }
}
