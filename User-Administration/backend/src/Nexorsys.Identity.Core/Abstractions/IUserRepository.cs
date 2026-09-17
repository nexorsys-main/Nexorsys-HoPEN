using Nexorsys.Identity.Core;

namespace Nexorsys.Identity.Core.Abstractions
{
	public interface IUserRepository
	{
		Task<User?> GetByIdAsync(Guid id);
		Task<User?> GetByAdGuidAsync(string adGuid);
		Task<User?> GetBySamAccountNameAsync(string samAccountName);
		Task<IEnumerable<User>> GetAllAsync(int page, int pageSize);
		Task AddAsync(User user);
		Task UpdateAsync(User user);
		Task DeleteAsync(Guid id);
		Task<int> CountAsync();
		Task<IEnumerable<User>> SearchAsync(string? query, string? department = null);
	}
}
