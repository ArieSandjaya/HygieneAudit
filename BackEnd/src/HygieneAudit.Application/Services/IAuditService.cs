using HygieneAudit.Application.DTOs;
using HygieneAudit.Domain.DTOs;

namespace HygieneAudit.Application.Services;

public interface IAuditService
{
    Task<AuditResponse> CreateAuditAsync(CreateAuditRequest request);
    Task<AuditResponse?> GetAuditAsync(string id);
    Task<string?> GetPhotoUrlAsync(int photoId);
    Task<IEnumerable<AuditResponse>> GetAuditsAsync(int picId, bool isAdmin);
    // Returns the stored values (filenames / data URLs) of any photos that were
    // removed, so the caller can delete the corresponding files from disk.
    Task<IReadOnlyList<string>> SaveAuditItemAsync(string auditId, int templateId, AuditItemUpdate update);
    Task<int> AddPhotoAsync(string auditId, int templateId, string filename);
    Task SubmitAuditAsync(string id);
    Task SaveDraftAsync(string id);
    // Deletes a DRAFT audit (with its items/photos). Returns the stored photo
    // values so the caller can delete the files from disk. Throws if not a draft.
    Task<IReadOnlyList<string>> DeleteDraftAuditAsync(string id);
    Task<TenantHistory> GetTenantHistoryAsync(int tenantId);
    Task<ExcelReportDto> GetExcelReportAsync(string? status, string? type, string? search);
    Task<byte[]> ExportExcelAsync(string? status, string? type, string? search, string? uploadsFolder = null);
}
