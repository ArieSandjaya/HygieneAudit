namespace HygieneAudit.Domain.DTOs;

public class FollowUpStat
{
    public string AuditId { get; set; } = string.Empty;
    public int Count { get; set; }
    public DateTime LastAt { get; set; }
}
