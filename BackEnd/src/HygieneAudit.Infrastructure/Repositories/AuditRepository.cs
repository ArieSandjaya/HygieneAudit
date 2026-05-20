using HygieneAudit.Domain.DTOs;
using HygieneAudit.Domain.Entities;
using HygieneAudit.Domain.Interfaces;
using HygieneAudit.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HygieneAudit.Infrastructure.Repositories;

public class AuditRepository : Repository<Audit>, IAuditRepository
{
    public AuditRepository(HygieneAuditDbContext context) : base(context) { }

    public async Task<Audit?> GetByIdWithItemsAsync(string id)
    {
        return await _context.Audits
            .Include(a => a.Tenant)
            .Include(a => a.Pic)
            .Include(a => a.Items)
                .ThenInclude(i => i.Photos)
            .FirstOrDefaultAsync(a => a.Id == id);
    }

    public async Task<IEnumerable<Audit>> GetByTenantIdAsync(int tenantId)
    {
        return await _context.Audits
            .Include(a => a.Items)
            .Where(a => a.TenantId == tenantId)
            .OrderByDescending(a => a.Date)
            .ToListAsync();
    }

    public async Task<IEnumerable<Audit>> GetLatestPerTenantAsync()
    {
        var latestIds = await _context.Audits
            .GroupBy(a => a.TenantId)
            .Select(g => g.OrderByDescending(a => a.Date).First().Id)
            .ToListAsync();

        return await _context.Audits
            .Include(a => a.Tenant)
            .Include(a => a.Pic)
            .Include(a => a.Items)
            .Where(a => latestIds.Contains(a.Id))
            .OrderByDescending(a => a.Date)
            .ToListAsync();
    }

    public async Task<IEnumerable<Audit>> GetFilteredAsync(string? status, string? type, string? search)
    {
        var query = _context.Audits
            .Include(a => a.Tenant)
            .Include(a => a.Pic)
            .Include(a => a.Items)
            .AsQueryable();

        if (!string.IsNullOrEmpty(status) && status != "all")
            query = query.Where(a => a.Status.ToString() == status);

        if (!string.IsNullOrEmpty(type) && type != "all")
        {
            bool isGas = type == "gas";
            query = query.Where(a => a.Tenant.UsesGas == isGas);
        }

        if (!string.IsNullOrEmpty(search))
            query = query.Where(a => a.Tenant.Name.Contains(search));

        var audits = await query.ToListAsync();
        return audits
            .GroupBy(a => a.TenantId)
            .Select(g => g.OrderByDescending(a => a.Date).First())
            .OrderByDescending(a => a.Date);
    }

    public async Task<TenantHistory> GetTenantHistoryAsync(int tenantId)
    {
        var audits = await _context.Audits
            .Include(a => a.Items)
            .Include(a => a.Pic)
            .Where(a => a.TenantId == tenantId)
            .OrderByDescending(a => a.Date)
            .ToListAsync();

        var tenant = await _context.Tenants.FindAsync(tenantId);

        if (!audits.Any())
        {
            return new TenantHistory
            {
                TenantId = tenantId,
                TenantName = tenant?.Name ?? "Unknown",
                UsesGas = tenant?.UsesGas ?? false,
                TotalAudits = 0
            };
        }

        var recent = audits.Take(6).Select(a =>
        {
            var total = a.Items.Count;
            var pass = a.Items.Count(i => i.Status == AuditItemStatus.Pass);
            return new RecentAuditDto
            {
                Id = a.Id,
                Date = a.Date,
                PicName = a.Pic?.Name ?? "Unknown",
                TotalItems = total,
                PassItems = pass,
                FailItems = total - pass,
                PassRate = total > 0 ? Math.Round((double)pass / total * 100, 0) : 0
            };
        }).ToList();

        var avgPass = audits.Average(a =>
        {
            var total = a.Items.Count;
            var pass = a.Items.Count(i => i.Status == AuditItemStatus.Pass);
            return total > 0 ? (double)pass / total * 100 : 0;
        });

        var lastAudit = audits.First();
        var daysSince = (DateTime.UtcNow - lastAudit.Date).Days;

        return new TenantHistory
        {
            TenantId = tenantId,
            TenantName = tenant?.Name ?? "Unknown",
            UsesGas = tenant?.UsesGas ?? false,
            TotalAudits = audits.Count,
            AveragePassRate = Math.Round(avgPass, 0),
            LastAuditDate = lastAudit.Date,
            DaysSinceLastAudit = daysSince == 0 ? "Hari ini" : 
                daysSince == 1 ? "Kemarin" : $"{daysSince} hari",
            RecentAudits = recent
        };
    }
}
