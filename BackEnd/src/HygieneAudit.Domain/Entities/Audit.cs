namespace HygieneAudit.Domain.Entities;

public class Audit
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public DateTime Date { get; set; }
    public int TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;
    public int PicId { get; set; }
    public User Pic { get; set; } = null!;
    public bool IsGas { get; set; }
    public AuditStatus Status { get; set; } = AuditStatus.Draft;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }

    public ICollection<AuditItem> Items { get; set; } = new List<AuditItem>();
}

public enum AuditStatus { Draft, Completed }

public class AuditItem
{
    public int Id { get; set; }
    public string AuditId { get; set; } = string.Empty;
    public Audit Audit { get; set; } = null!;
    public int TemplateId { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public AuditItemStatus? Status { get; set; }
    public string? Note { get; set; }
    public ICollection<AuditItemPhoto> Photos { get; set; } = new List<AuditItemPhoto>();
}

public enum AuditItemStatus { Pass, Fail }

public class AuditItemPhoto
{
    public int Id { get; set; }
    public int AuditItemId { get; set; }
    public AuditItem AuditItem { get; set; } = null!;
    public string PhotoUrl { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
}
