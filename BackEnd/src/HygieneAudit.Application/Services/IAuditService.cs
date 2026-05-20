using HygieneAudit.Application.DTOs;
using HygieneAudit.Domain.DTOs;

namespace HygieneAudit.Application.Services;

public interface IAuditService
{
    Task<AuditResponse> CreateAuditAsync(CreateAuditRequest request);
    Task<AuditResponse?> GetAuditAsync(string id);
    Task<IEnumerable<AuditResponse>> GetAuditsAsync(int picId, bool isAdmin);
    Task SaveAuditItemAsync(string auditId, int templateId, AuditItemUpdate update);
    Task SubmitAuditAsync(string id);
    Task SaveDraftAsync(string id);
    Task<TenantHistory> GetTenantHistoryAsync(int tenantId);
    Task<ExcelReportDto> GetExcelReportAsync(string? status, string? type, string? search);
    Task<byte[]> ExportExcelAsync(ExcelReportDto report);
}
