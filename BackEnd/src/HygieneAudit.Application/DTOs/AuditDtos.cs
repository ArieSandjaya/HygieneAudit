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
    public AuditItemStatus? Status { get; set; }
    public string? Note { get; set; }
    public List<string>? Photos { get; set; }
}
