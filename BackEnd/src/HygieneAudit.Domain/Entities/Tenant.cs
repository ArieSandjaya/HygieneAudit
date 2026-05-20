namespace HygieneAudit.Domain.Entities;

public class Tenant
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool UsesGas { get; set; }
    public string? Floor { get; set; }
    public string? Category { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;

    public ICollection<Audit> Audits { get; set; } = new List<Audit>();
}
