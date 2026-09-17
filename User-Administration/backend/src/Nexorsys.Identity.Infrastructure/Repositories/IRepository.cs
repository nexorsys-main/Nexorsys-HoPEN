using Nexorsys.Identity.Core;

namespace Nexorsys.Identity.Infrastructure.Repositories
{
	// IUserRepository has been moved to Nexorsys.Identity.Core.Abstractions

	public interface IUserPinRepository
	{
		UserPin? GetUserPinByBadgeUid(string badgeUid);
		IEnumerable<UserPin> GetUserPinsByUserId(Guid userId);
		void AddUserPin(UserPin userPin);
		void UpdateUserPin(UserPin userPin);
		void DeleteUserPin(Guid userPinId);
	}

	public interface IApplicationRepository
	{
		Application? GetApplicationByClientId(string clientId);
		IEnumerable<Application> GetAllApplications();
		void AddApplication(Application application);
		void UpdateApplication(Application application);
		void DeleteApplication(Guid applicationId);
	}

	public interface IUserPermissionRepository
	{
		IEnumerable<UserPermission> GetUserPermissionsByUserId(Guid userId);
		IEnumerable<UserPermission> GetUserPermissionsByApplicationId(Guid applicationId);
		void AddUserPermission(UserPermission userPermission);
		void RemoveUserPermission(Guid userPermissionId);
	}

	public interface IAuditLogRepository
	{
		void AddAuditLog(AuditLog auditLog);
		IEnumerable<AuditLog> GetAuditLogs(DateTimeOffset from, DateTimeOffset to);
	}

	public interface IWorkflowRepository
	{
		Workflow? GetWorkflowById(Guid workflowId);
		IEnumerable<Workflow> GetWorkflowsByUser(Guid userId);
		IEnumerable<Workflow> GetWorkflowsByStatus(string status);
		void AddWorkflow(Workflow workflow);
		void UpdateWorkflow(Workflow workflow);
	}

	public interface IKioskSessionRepository
	{
		KioskSession? GetSessionByToken(string sessionToken);
		KioskSession? GetSessionById(Guid sessionId);
		void AddSession(KioskSession session);
		void UpdateSession(KioskSession session);
		void RemoveSession(Guid sessionId);
	}
}
