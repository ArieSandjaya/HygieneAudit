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
            .Where(t => !t.RequiresGas || (t.RequiresGas && request.IsGas))
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
        var audit = await _unitOfWork.Audits.GetByIdWithItemsAsync(id);
        return audit == null ? null : AuditResponse.FromEntity(audit);
    }

    public async Task SaveAuditItemAsync(string auditId, int templateId, AuditItemUpdate update)
    {
        var audit = await _unitOfWork.Audits.GetByIdWithItemsAsync(auditId);
        if (audit == null) throw new NotFoundException("Audit not found");

        var item = audit.Items.FirstOrDefault(i => i.TemplateId == templateId);
        if (item == null) throw new NotFoundException("Item not found");

        item.Status = update.Status;
        item.Note = update.Note;

        await _unitOfWork.SaveChangesAsync();
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

        audit.Status = AuditStatus.Draft;
        await _unitOfWork.SaveChangesAsync();
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
                TenantName = audit.Tenant.Name,
                UsesGas = audit.Tenant.UsesGas,
                Date = audit.Date,
                PicName = audit.Pic.Name,
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

    public async Task<byte[]> ExportExcelAsync(ExcelReportDto report)
    {
        var csv = new System.Text.StringBuilder();
        csv.AppendLine("No,Tenant,Type,Tanggal,PIC,Status,Pass Rate,Total,Pass,Fail,Catatan");

        foreach (var row in report.Rows)
        {
            csv.AppendLine($"{row.No},{row.TenantName},{(row.UsesGas ? "Gas" : "Non-Gas")},{row.Date:yyyy-MM-dd},{row.PicName},{row.Status},{row.PassRate}%,{row.TotalItems},{row.PassItems},{row.FailItems},\"{row.FailNotes}\"");
        }

        csv.AppendLine($",TOTAL / RATA-RATA,,,,,{report.Summary.AveragePassRate}%,{report.Summary.TotalItems},{report.Summary.TotalPass},{report.Summary.TotalFail},{report.Summary.TenantCount} Tenant");

        return System.Text.Encoding.UTF8.GetBytes(csv.ToString());
    }
}
