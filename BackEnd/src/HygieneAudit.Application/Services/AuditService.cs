using HygieneAudit.Application.DTOs;
using HygieneAudit.Application.Exceptions;
using HygieneAudit.Domain.DTOs;
using HygieneAudit.Domain.Entities;
using HygieneAudit.Domain.Interfaces;

namespace HygieneAudit.Application.Services;

public class AuditService : IAuditService
{
    private readonly IUnitOfWork _unitOfWork;

    public AuditService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<AuditResponse> CreateAuditAsync(CreateAuditRequest request)
    {
        var templates = await _unitOfWork.Templates.GetAllAsync();
        var filteredTemplates = templates
            .Where(t => t.IsActive)
            .Where(t => !t.RequiresGas || (t.RequiresGas && request.IsGas))
            .OrderBy(t => t.Category)
            .ThenBy(t => t.DisplayOrder)
            .ThenBy(t => t.Name)
            .ToList();

        var audit = new Audit
        {
            Date = request.Date,
            TenantId = request.TenantId,
            PicId = request.PicId,
            IsGas = request.IsGas,
            Items = filteredTemplates.Select(t => new AuditItem
            {
                TemplateId = t.Id,
                Category = t.Category,
                Name = t.Name
            }).ToList()
        };

        await _unitOfWork.Audits.AddAsync(audit);
        await _unitOfWork.SaveChangesAsync();

        var saved = await _unitOfWork.Audits.GetByIdWithItemsAsync(audit.Id);
        return AuditResponse.FromEntity(saved!);
    }

    public async Task<AuditResponse?> GetAuditAsync(string id)
    {
        var audit = await _unitOfWork.Audits.GetByIdForDisplayAsync(id);
        return audit == null ? null : AuditResponse.FromEntity(audit);
    }

    public async Task<IEnumerable<AuditResponse>> GetAuditsAsync(int picId, bool isAdmin)
    {
        var audits = await _unitOfWork.Audits.GetRecentAsync(picId, isAdmin);
        return audits.Select(AuditResponse.FromEntity);
    }

    public async Task<IReadOnlyList<string>> SaveAuditItemAsync(string auditId, int templateId, AuditItemUpdate update)
    {
        var audit = await _unitOfWork.Audits.GetByIdWithItemsAsync(auditId);
        if (audit == null) throw new NotFoundException("Audit not found");
        if (audit.Status == AuditStatus.Completed)
            throw new ValidationException("Audit sudah selesai dan tidak dapat diedit.");

        var item = audit.Items.FirstOrDefault(i => i.TemplateId == templateId);
        if (item == null) throw new NotFoundException("Item not found");

        item.Status = update.Status?.ToLowerInvariant() switch
        {
            "pass" => AuditItemStatus.Pass,
            "fail" => AuditItemStatus.Fail,
            _      => (AuditItemStatus?)null
        };
        item.Note = update.Note;

        var removed = new List<string>();
        if (update.Photos != null)
        {
            // Incoming entries are a mix of references to already-saved photos
            // (".../api/audits/photos/{id}") and brand-new base64 data URLs.
            // Keep referenced photos, add new ones, drop the rest — never re-store
            // a reference URL as if it were image data.
            var keepIds = new HashSet<int>();
            var newPhotos = new List<string>();
            foreach (var entry in update.Photos)
            {
                if (string.IsNullOrWhiteSpace(entry)) continue;
                var idx = entry.LastIndexOf("/photos/", StringComparison.OrdinalIgnoreCase);
                if (idx >= 0 && int.TryParse(entry.Substring(idx + "/photos/".Length), out var pid))
                    keepIds.Add(pid);
                else
                    newPhotos.Add(entry);
            }

            foreach (var p in item.Photos.ToList())
                if (!keepIds.Contains(p.Id))
                {
                    removed.Add(p.PhotoUrl);
                    item.Photos.Remove(p);
                }

            foreach (var url in newPhotos)
                item.Photos.Add(new AuditItemPhoto { PhotoUrl = url });
        }

        await _unitOfWork.SaveChangesAsync();
        return removed;
    }

    public async Task<string?> GetPhotoUrlAsync(int photoId)
        => await _unitOfWork.Audits.GetPhotoUrlAsync(photoId);

    public async Task<int> AddPhotoAsync(string auditId, int templateId, string filename)
    {
        var audit = await _unitOfWork.Audits.GetByIdWithItemsAsync(auditId);
        if (audit == null) throw new NotFoundException("Audit not found");
        if (audit.Status == AuditStatus.Completed)
            throw new ValidationException("Audit sudah selesai dan tidak dapat diedit.");

        var item = audit.Items.FirstOrDefault(i => i.TemplateId == templateId);
        if (item == null) throw new NotFoundException("Item not found");

        var photo = new AuditItemPhoto { PhotoUrl = filename };
        item.Photos.Add(photo);
        await _unitOfWork.SaveChangesAsync();
        return photo.Id;
    }

    public async Task SubmitAuditAsync(string id)
    {
        var audit = await _unitOfWork.Audits.GetByIdWithItemsAsync(id);
        if (audit == null) throw new NotFoundException("Audit not found");

        var uncheckedItems = audit.Items.Where(i => i.Status == null).ToList();
        if (uncheckedItems.Any())
            throw new ValidationException($"{uncheckedItems.Count} items belum dicek!");

        var failWithoutNote = audit.Items
            .Where(i => i.Status == AuditItemStatus.Fail && string.IsNullOrWhiteSpace(i.Note))
            .ToList();
        if (failWithoutNote.Any())
            throw new ValidationException("Catatan wajib diisi untuk item FAIL!");

        audit.Status = AuditStatus.Completed;
        audit.CompletedAt = DateTime.UtcNow;

        await _unitOfWork.SaveChangesAsync();
    }

    public async Task SaveDraftAsync(string id)
    {
        var audit = await _unitOfWork.Audits.GetByIdWithItemsAsync(id);
        if (audit == null) throw new NotFoundException("Audit not found");
        if (audit.Status == AuditStatus.Completed)
            throw new ValidationException("Audit sudah selesai dan tidak dapat diubah kembali ke draft.");

        audit.Status = AuditStatus.Draft;
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task<IReadOnlyList<string>> DeleteDraftAuditAsync(string id)
    {
        var audit = await _unitOfWork.Audits.GetByIdWithItemsAsync(id);
        if (audit == null) throw new NotFoundException("Audit not found");
        if (audit.Status != AuditStatus.Draft)
            throw new ValidationException("Hanya audit berstatus draft yang dapat dihapus.");

        // Collect photo files so the caller can remove them from disk after the
        // database rows are gone (items & photos cascade-delete with the audit).
        var photos = audit.Items
            .SelectMany(i => i.Photos)
            .Select(p => p.PhotoUrl)
            .Where(u => !string.IsNullOrWhiteSpace(u))
            .ToList();

        await _unitOfWork.Audits.DeleteAsync(audit);
        await _unitOfWork.SaveChangesAsync();
        return photos;
    }

    public async Task<TenantHistory> GetTenantHistoryAsync(int tenantId)
    {
        return await _unitOfWork.Audits.GetTenantHistoryAsync(tenantId);
    }

    public async Task<ExcelReportDto> GetExcelReportAsync(string? status, string? type, string? search)
    {
        var audits = await _unitOfWork.Audits.GetFilteredAsync(status, type, search);

        var rows = new List<ExcelReportRow>();
        int no = 1;

        foreach (var audit in audits)
        {
            var total = audit.Items.Count;
            var pass = audit.Items.Count(i => i.Status == AuditItemStatus.Pass);
            var fail = audit.Items.Count(i => i.Status == AuditItemStatus.Fail);
            var rate = total > 0 ? Math.Round((double)pass / total * 100, 0) : 0;

            var failNotes = string.Join("; ", audit.Items
                .Where(i => i.Status == AuditItemStatus.Fail && !string.IsNullOrEmpty(i.Note))
                .Select(i => i.Note));

            rows.Add(new ExcelReportRow
            {
                No = no++,
                TenantName = audit.Tenant?.Name ?? string.Empty,
                UsesGas = audit.Tenant?.UsesGas ?? false,
                Date = audit.Date,
                PicName = audit.Pic?.Name ?? string.Empty,
                Status = audit.Status.ToString(),
                TotalItems = total,
                PassItems = pass,
                FailItems = fail,
                PassRate = rate,
                FailNotes = failNotes
            });
        }

        var totalItems = rows.Sum(r => r.TotalItems);
        var totalPass = rows.Sum(r => r.PassItems);

        return new ExcelReportDto
        {
            Rows = rows,
            Summary = new ExcelReportSummary
            {
                AveragePassRate = totalItems > 0 ? Math.Round((double)totalPass / totalItems * 100, 0) : 0,
                TotalItems = totalItems,
                TotalPass = totalPass,
                TotalFail = rows.Sum(r => r.FailItems),
                TenantCount = rows.Count
            }
        };
    }

    public async Task<byte[]> ExportExcelAsync(string? status, string? type, string? search, string? uploadsFolder = null)
    {
        var audits = await _unitOfWork.Audits.GetFilteredAsync(status, type, search);
        return ExcelBuilder.Build(audits, uploadsFolder);
    }
}
