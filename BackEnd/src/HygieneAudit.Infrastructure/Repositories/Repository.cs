using HygieneAudit.Domain.Interfaces;
using HygieneAudit.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HygieneAudit.Infrastructure.Repositories;

public class Repository<T> : IRepository<T> where T : class
{
    protected readonly HygieneAuditDbContext _context;
    protected readonly DbSet<T> _dbSet;

    public Repository(HygieneAuditDbContext context)
    {
        _context = context;
        _dbSet = context.Set<T>();
    }

    public virtual async Task<T?> GetByIdAsync(int id) => await _dbSet.FindAsync(id);
    public virtual async Task<IEnumerable<T>> GetAllAsync() => await _dbSet.ToListAsync();

    public virtual async Task<T> AddAsync(T entity)
    {
        await _dbSet.AddAsync(entity);
        return entity;
    }

    public virtual Task UpdateAsync(T entity)
    {
        _dbSet.Update(entity);
        return Task.CompletedTask;
    }

    public virtual Task DeleteAsync(T entity)
    {
        _dbSet.Remove(entity);
        return Task.CompletedTask;
    }

    public virtual async Task<bool> ExistsAsync(int id)
    {
        return await _dbSet.FindAsync(id) != null;
    }
}
