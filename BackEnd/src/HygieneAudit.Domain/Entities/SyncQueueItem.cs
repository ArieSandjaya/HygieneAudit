namespace HygieneAudit.Domain.Entities;

public class SyncQueueItem
{
    public int Id { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public bool IsSynced { get; set; }
    public int RetryCount { get; set; }
    public string? ErrorMessage { get; set; }
}
