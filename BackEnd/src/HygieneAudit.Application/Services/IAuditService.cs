using HygieneAudit.Application.DTOs;
using HygieneAudit.Domain.DTOs;
using HygieneAudit.Domain.Entities;

namespace HygieneAudit.Application.Services;

public interface IAuditService
{
    Task<Audit> CreateAuditAsync(CreateAuditRequest request);
    Task<Audit?> GetAuditAsync(string id);
    Task SaveAuditItemAsync(string auditId, int templateId, AuditItemUpdate update);
    Task SubmitAuditAsync(string id);
    Task SaveDraftAsync(string id);
    Task<TenantHistory> GetTenantHistoryAsync(int tenantId);
    Task<ExcelReportDto> GetExcelReportAsync(string? status, string? type, string? search);
    Task<byte[]> ExportExcelAsync(ExcelReportDto report);
}
