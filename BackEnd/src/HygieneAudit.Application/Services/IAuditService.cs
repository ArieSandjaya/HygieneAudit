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
    // ---- Follow up ----
    Task<IEnumerable<FollowUpAuditSummary>> GetFollowUpAuditsAsync();
    Task<FollowUpDetailResponse?> GetFollowUpDetailAsync(string auditId);
    // Mencatat follow up untuk satu item FAIL. Bila hasilnya Pass, status item berubah jadi Pass
    // (nilai audit ikut ter-update); riwayat selalu tersimpan.
    Task<FollowUpResponse> AddFollowUpAsync(string auditId, int auditItemId, int picId, AddFollowUpRequest request);
    Task<string?> GetFollowUpPhotoUrlAsync(int photoId);
    // Laporan follow up. status: all | unresolved | overdue | resolved.
    Task<FollowUpReportDto> GetFollowUpReportAsync(string? status, string? type, string? search, DateTime? from, DateTime? to);
    Task<byte[]> ExportFollowUpReportAsync(string? status, string? type, string? search, DateTime? from, DateTime? to);
    Task<byte[]> ExportExcelAsync(string? status, string? type, string? search, string? uploadsFolder = null);
}
