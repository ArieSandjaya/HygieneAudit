﻿using HygieneAudit.Domain.DTOs;
using HygieneAudit.Domain.Entities;

namespace HygieneAudit.Domain.Interfaces;

public interface IAuditRepository : IRepository<Audit>
{
    Task<Audit?> GetByIdWithItemsAsync(string id);
    Task<IEnumerable<Audit>> GetByTenantIdAsync(int tenantId);
    Task<IEnumerable<Audit>> GetLatestPerTenantAsync();
    Task<IEnumerable<Audit>> GetFilteredAsync(string? status, string? type, string? search);
    Task<TenantHistory> GetTenantHistoryAsync(int tenantId);
}
