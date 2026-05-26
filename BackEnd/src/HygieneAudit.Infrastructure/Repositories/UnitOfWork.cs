using HygieneAudit.Domain.Entities;
using HygieneAudit.Domain.Interfaces;
using HygieneAudit.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Storage;

namespace HygieneAudit.Infrastructure.Repositories;

public class UnitOfWork : IUnitOfWork
{
    private readonly HygieneAuditDbContext _context;
    private IDbContextTransaction? _transaction;

    public IAuditRepository Audits { get; }
    public ITenantRepository Tenants { get; }
    public IRepository<User> Users { get; }
    public IRepository<ChecklistTemplate> Templates { get; }
    public IRepository<SyncQueueItem> SyncQueue { get; }

    public UnitOfWork(HygieneAuditDbContext context)
    {
        _context = context;
        Audits = new AuditRepository(context);
        Tenants = new TenantRepository(context);
        Users = new Repository<User>(context);
        Templates = new Repository<ChecklistTemplate>(context);
        SyncQueue = new Repository<SyncQueueItem>(context);
    }

    public async Task<int> SaveChangesAsync() => await _context.SaveChangesAsync();

    public async Task BeginTransactionAsync()
    {
        _transaction = await _context.Database.BeginTransactionAsync();
    }

    public async Task CommitTransactionAsync()
    {
        if (_transaction != null)
        {
            await _transaction.CommitAsync();
            _transaction.Dispose(); // IAsyncDisposable not available in EF Core 3.1 on .NET Framework
            _transaction = null;
        }
    }

    public async Task RollbackTransactionAsync()
    {
        if (_transaction != null)
        {
            await _transaction.RollbackAsync();
            _transaction.Dispose();
            _transaction = null;
        }
    }

    public void Dispose()
    {
        _context.Dispose();
        _transaction?.Dispose();
    }
}
