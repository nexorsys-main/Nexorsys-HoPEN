using Nexorsys.Identity.Core;

namespace Nexorsys.Identity.Core.Abstractions
{
	public interface IUserService
	{
		Task<IEnumerable<User>> GetUsersAsync(string? department);
		Task<User?> GetUserByIdAsync(Guid id);
		Task<User> CreateUserAsync(User user);
		Task<User?> UpdateUserAsync(Guid id, User user);
		Task<bool> DeleteUserAsync(Guid id);
	}
}
