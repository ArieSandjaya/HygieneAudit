using HygieneAudit.Domain.Entities;
using HygieneAudit.Domain.Interfaces;
using HygieneAudit.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HygieneAudit.Infrastructure.Repositories;

public class TenantRepository : Repository<Tenant>, ITenantRepository
{
    public TenantRepository(HygieneAuditDbContext context) : base(context) { }

    public async Task<Tenant?> GetByNameAsync(string name)
    {
        return await _context.Tenants
            .FirstOrDefaultAsync(t => t.Name == name && t.IsActive);
    }

    public async Task<IEnumerable<Tenant>> GetActiveAsync()
    {
        return await _context.Tenants
            .Where(t => t.IsActive)
            .OrderBy(t => t.Name)
            .ToListAsync();
    }
}
