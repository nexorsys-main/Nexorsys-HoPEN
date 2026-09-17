using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Nexorsys.Identity.Core.Abstractions;

namespace Nexorsys.Identity.Infrastructure.Repositories
{
	public interface IGenericRepository<T> where T : class
	{
		T? GetById(Guid id);
		IEnumerable<T> GetAll();
		void Add(T entity);
		void Update(T entity);
		void Delete(Guid id);
	}

	public class GenericRepository<T> : IGenericRepository<T> where T : class
	{
		private readonly AppDbContext _context;
		private readonly DbSet<T> _dbSet;
		private readonly IOrganizationContext _organization;

		public GenericRepository(AppDbContext context, IOrganizationContext organization)
		{
			_context = context;
			_dbSet = context.Set<T>();
			_organization = organization;
		}

		public T? GetById(Guid id)
		{
			return TenantSet().FirstOrDefault(e => EF.Property<Guid>(e, nameof(Nexorsys.Identity.Core.User.Id)) == id);
		}

		public IEnumerable<T> GetAll()
		{
			return TenantSet().ToList();
		}

		public void Add(T entity)
		{
			EnsureTenant(entity);
			_dbSet.Add(entity);
			_context.SaveChanges();
		}

		public void Update(T entity)
		{
			EnsureTenant(entity);
			_dbSet.Update(entity);
			_context.SaveChanges();
		}

		public void Delete(Guid id)
		{
			var entity = TenantSet().FirstOrDefault(e => EF.Property<Guid>(e, nameof(Nexorsys.Identity.Core.User.Id)) == id);
			if (entity != null)
			{
				_dbSet.Remove(entity);
				_context.SaveChanges();
			}
		}

		private IQueryable<T> TenantSet()
		{
			var organizationId = CurrentOrganizationId();
			return _dbSet.Where(entity => EF.Property<Guid>(entity, nameof(Nexorsys.Identity.Core.User.OrganizationId)) == organizationId);
		}

		private Guid CurrentOrganizationId() => _organization.OrganizationId is { } organizationId && organizationId != Guid.Empty
			? organizationId
			: throw new InvalidOperationException("Repository organization context is unavailable.");

		private void EnsureTenant(T entity)
		{
			var property = typeof(T).GetProperty(nameof(Nexorsys.Identity.Core.User.OrganizationId));
			if (property?.PropertyType != typeof(Guid) || property.GetValue(entity) is not Guid organizationId ||
				organizationId != CurrentOrganizationId())
				throw new InvalidOperationException("Repository operation is outside the current organization.");
		}
	}
}
