using HygieneAudit.Domain.Entities;

namespace HygieneAudit.Domain.Interfaces;

public interface IUnitOfWork : IDisposable
{
    IAuditRepository Audits { get; }
    ITenantRepository Tenants { get; }
    IRepository<User> Users { get; }
    IRepository<ChecklistTemplate> Templates { get; }
    IRepository<SyncQueueItem> SyncQueue { get; }

    Task<int> SaveChangesAsync();
    Task BeginTransactionAsync();
    Task CommitTransactionAsync();
    Task RollbackTransactionAsync();
}
