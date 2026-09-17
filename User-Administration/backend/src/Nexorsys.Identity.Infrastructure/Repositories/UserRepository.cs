using Microsoft.EntityFrameworkCore;
using Nexorsys.Identity.Core;
using Nexorsys.Identity.Core.Abstractions;

namespace Nexorsys.Identity.Infrastructure.Repositories
{
	public class UserRepository : IUserRepository
	{
		private readonly AppDbContext _context;

		public UserRepository(AppDbContext context)
		{
			_context = context;
		}

		public async Task<User?> GetByIdAsync(Guid id)
		{
			return await TenantUsers()
				.Include(u => u.UserPins)
				.Include(u => u.UserPermissions!)
					.ThenInclude(p => p.Application)
				.Include(u => u.Devices)
				.FirstOrDefaultAsync(u => u.Id == id);
		}

		public async Task<User?> GetByAdGuidAsync(string adGuid)
		{
			return await TenantUsers().FirstOrDefaultAsync(u => u.AdGuid == adGuid);
		}

		public async Task<User?> GetBySamAccountNameAsync(string samAccountName)
		{
			return await TenantUsers().FirstOrDefaultAsync(u => u.SamAccountName == samAccountName);
		}

		public async Task<IEnumerable<User>> GetAllAsync(int page, int pageSize)
		{
			return await TenantUsers()
				.OrderBy(u => u.LastName)
				.Skip(checked(Math.Max(0, page - 1) * Math.Max(0, pageSize)))
				.Take(pageSize)
				.ToListAsync();
		}

		public async Task AddAsync(User user)
		{
			EnsureTenant(user.OrganizationId);
			await _context.Users.AddAsync(user);
			await _context.SaveChangesAsync();
		}

		public async Task UpdateAsync(User user)
		{
			EnsureTenant(user.OrganizationId);
			await _context.SaveChangesAsync();
		}

		public async Task DeleteAsync(Guid id)
		{
			var user = await TenantUsers().FirstOrDefaultAsync(u => u.Id == id);
			if (user != null)
			{
				_context.Users.Remove(user);
				await _context.SaveChangesAsync();
			}
		}

		public async Task<int> CountAsync()
		{
			return await TenantUsers().CountAsync();
		}

		public async Task<IEnumerable<User>> SearchAsync(string? query, string? department = null)
		{
			var dbQuery = TenantUsers();

			if (!string.IsNullOrWhiteSpace(query))
			{
				var lowerQuery = query.ToLower();
				dbQuery = dbQuery.Where(u => u.SamAccountName.ToLower().Contains(lowerQuery) ||
											(u.DisplayName != null && u.DisplayName.ToLower().Contains(lowerQuery)) ||
											(u.Email != null && u.Email.ToLower().Contains(lowerQuery)));
			}

			if (!string.IsNullOrWhiteSpace(department))
			{
				var lowerDept = department.ToLower();
				dbQuery = dbQuery.Where(u => u.Department != null && u.Department.ToLower().Contains(lowerDept));
			}

			return await dbQuery
				.OrderBy(u => u.LastName)
				.Take(50)
				.ToListAsync();
		}

		private IQueryable<User> TenantUsers() => _context.Users.Where(u =>
			_context.CurrentOrganizationId.HasValue && u.OrganizationId == _context.CurrentOrganizationId.Value);

		private void EnsureTenant(Guid organizationId)
		{
			if (!_context.CurrentOrganizationId.HasValue || organizationId != _context.CurrentOrganizationId.Value)
				throw new InvalidOperationException("User repository operation is outside the current organization.");
		}
	}
}
