using Nexorsys.Identity.Core;
using Nexorsys.Identity.Infrastructure;

namespace Nexorsys.Identity.API.Services;

public interface IUserService
{
	IEnumerable<User> GetUsers(string? department);
	User? GetUserById(Guid id);
	User CreateUser(CreateUserDto createUserDto);
	User? UpdateUser(Guid id, UpdateUserDto updateUserDto);
	bool DeleteUser(Guid id);
}

public class UserService : IUserService
{
	private readonly AppDbContext _context;

	public UserService(AppDbContext context)
	{
		_context = context;
	}

	public IEnumerable<User> GetUsers(string? department)
	{
		var query = _context.Users.AsQueryable();
		if (!string.IsNullOrEmpty(department))
		{
			query = query.Where(u => u.Department == department);
		}
		return query.ToList();
	}

	public User? GetUserById(Guid id)
	{
		return _context.Users.Find(id);
	}

	public User CreateUser(CreateUserDto createUserDto)
	{
		var user = new User
		{
			Id = Guid.NewGuid(),
			SamAccountName = createUserDto.SamAccountName,
			DistinguishedName = createUserDto.DistinguishedName,
			DisplayName = createUserDto.DisplayName,
			Email = createUserDto.Email,
			Department = createUserDto.Department,
			IsActive = true,
			IsLocalProfile = createUserDto.IsLocalProfile,
			CreatedAt = DateTime.UtcNow
		};
		_context.Users.Add(user);
		_context.SaveChanges();
		return user;
	}

	public User? UpdateUser(Guid id, UpdateUserDto updateUserDto)
	{
		var user = _context.Users.Find(id);
		if (user == null) return null;

		user.DisplayName = updateUserDto.DisplayName ?? user.DisplayName;
		user.Email = updateUserDto.Email ?? user.Email;
		user.Department = updateUserDto.Department ?? user.Department;
		user.UpdatedAt = DateTime.UtcNow;

		_context.SaveChanges();
		return user;
	}

	public bool DeleteUser(Guid id)
	{
		var user = _context.Users.Find(id);
		if (user == null) return false;

		_context.Users.Remove(user);
		_context.SaveChanges();
		return true;
	}
}

public class CreateUserDto
{
	public string SamAccountName { get; set; } = string.Empty;
	public string DistinguishedName { get; set; } = string.Empty;
	public string DisplayName { get; set; } = string.Empty;
	public string Email { get; set; } = string.Empty;
	public string Department { get; set; } = string.Empty;
	public bool IsLocalProfile { get; set; }
}

public class UpdateUserDto
{
	public string? DisplayName { get; set; }
	public string? Email { get; set; }
	public string? Department { get; set; }
}
