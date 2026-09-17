using Nexorsys.Identity.Core;

namespace Nexorsys.Identity.Core.Abstractions
{
	public interface ILdapService
	{
		Task<bool> TestConnectionAsync();
		Task<bool> AuthenticateAsync(string username, string password);
		Task<User?> GetUserBySamAccountNameAsync(string samAccountName);
		Task<User?> GetUserByBadgeUidAsync(string badgeUid);
		Task<IEnumerable<User>> SearchUsersAsync(string query);
		Task<bool> IsUserInGroupAsync(string samAccountName, string groupName);
	}
}
