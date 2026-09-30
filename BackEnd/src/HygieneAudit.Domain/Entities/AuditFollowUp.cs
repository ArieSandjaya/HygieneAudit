namespace HygieneAudit.Domain.Entities;

// Satu catatan tindak lanjut (follow up) untuk satu item audit yang FAIL.
// Setiap follow up disimpan sebagai riwayat; bila Result = Pass maka
// AuditItem.Status ikut berubah menjadi Pass (nilai audit ter-update).
public class AuditFollowUp
{
    public int Id { get; set; }
    public int AuditItemId { get; set; }
    public AuditItem AuditItem { get; set; } = null!;
    public DateTime Date { get; set; }
    public int PicId { get; set; }
    public User Pic { get; set; } = null!;
    public AuditItemStatus Result { get; set; }
    public string Note { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<AuditFollowUpPhoto> Photos { get; set; } = new List<AuditFollowUpPhoto>();
}

public class AuditFollowUpPhoto
{
    public int Id { get; set; }
    public int AuditFollowUpId { get; set; }
    public AuditFollowUp AuditFollowUp { get; set; } = null!;
    public string PhotoUrl { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
}
