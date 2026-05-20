using HygieneAudit.Domain.Entities;

namespace HygieneAudit.Domain.Interfaces;

public interface ITenantRepository : IRepository<Tenant>
{
    Task<Tenant?> GetByNameAsync(string name);
    Task<IEnumerable<Tenant>> GetActiveAsync();
}
