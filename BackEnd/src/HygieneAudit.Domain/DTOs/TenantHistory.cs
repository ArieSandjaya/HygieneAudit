namespace HygieneAudit.Domain.DTOs;

public class TenantHistory
{
    public int TenantId { get; set; }
    public string TenantName { get; set; } = string.Empty;
    public bool UsesGas { get; set; }
    public int TotalAudits { get; set; }
    public double AveragePassRate { get; set; }
    public DateTime? LastAuditDate { get; set; }
    public string DaysSinceLastAudit { get; set; } = string.Empty;
    public List<RecentAuditDto> RecentAudits { get; set; } = new();
}

public class RecentAuditDto
{
    public string Id { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public string PicName { get; set; } = string.Empty;
    public int TotalItems { get; set; }
    public int PassItems { get; set; }
    public int FailItems { get; set; }
    public double PassRate { get; set; }
}
