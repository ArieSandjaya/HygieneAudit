using HygieneAudit.Domain.Entities;

namespace HygieneAudit.Application.DTOs;

public class CreateAuditRequest
{
    public DateTime Date { get; set; }
    public int TenantId { get; set; }
    public int PicId { get; set; }
    public bool IsGas { get; set; }
}

public class AuditItemUpdate
{
    public string? Status { get; set; }   // "Pass" / "Fail" / null
    public string? Note { get; set; }
    public List<string>? Photos { get; set; }
}

public class AuditResponse
{
    public string Id { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public int TenantId { get; set; }
    public string TenantName { get; set; } = string.Empty;
    public int PicId { get; set; }
    public string PicName { get; set; } = string.Empty;
    public bool IsGas { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public List<AuditItemResponse> Items { get; set; } = new();

    public static AuditResponse FromEntity(Audit audit) => new()
    {
        Id = audit.Id,
        Date = audit.Date,
        TenantId = audit.TenantId,
        TenantName = audit.Tenant?.Name ?? string.Empty,
        PicId = audit.PicId,
        PicName = audit.Pic?.Name ?? string.Empty,
        IsGas = audit.IsGas,
        Status = audit.Status.ToString().ToUpper(),
        CreatedAt = audit.CreatedAt,
        CompletedAt = audit.CompletedAt,
        Items = (audit.Items ?? Enumerable.Empty<AuditItem>()).Select(AuditItemResponse.FromEntity).ToList()
    };
}

public class AuditItemResponse
{
    public int Id { get; set; }
    public int TemplateId { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Status { get; set; }
    public string? Note { get; set; }
    public List<string> Photos { get; set; } = new();

    public static AuditItemResponse FromEntity(AuditItem item) => new()
    {
        Id = item.Id,
        TemplateId = item.TemplateId,
        Category = item.Category,
        Name = item.Name,
        Status = item.Status?.ToString().ToUpper(),
        Note = item.Note,
        Photos = item.Photos.Select(p => p.PhotoUrl).ToList()
    };
}
