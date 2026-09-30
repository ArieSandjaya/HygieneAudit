using HygieneAudit.Domain.Entities;

namespace HygieneAudit.Application.DTOs;

// Baris pada daftar menu Follow Up: satu audit COMPLETED yang belum 100% (atau pernah di-follow up).
public class FollowUpAuditSummary
{
    public string Id { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public int TenantId { get; set; }
    public string TenantName { get; set; } = string.Empty;
    public string PicName { get; set; } = string.Empty;
    public bool IsGas { get; set; }
    public int TotalItems { get; set; }
    public int PassCount { get; set; }
    public int FailCount { get; set; }
    public double PassRate { get; set; }
    public int FollowUpCount { get; set; }
    public DateTime? LastFollowUpAt { get; set; }
}

public class AddFollowUpRequest
{
    public DateTime? Date { get; set; }
    public string? Result { get; set; }   // "Pass" / "Fail"
    public string? Note { get; set; }
    public List<string>? Photos { get; set; }   // data URL (diubah jadi nama file oleh controller)
}

public class FollowUpResponse
{
    public int Id { get; set; }
    public DateTime Date { get; set; }
    public string PicName { get; set; } = string.Empty;
    public string Result { get; set; } = string.Empty;   // PASS / FAIL
    public string Note { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public List<string> Photos { get; set; } = new();

    public static FollowUpResponse FromEntity(AuditFollowUp f) => new()
    {
        Id = f.Id,
        Date = f.Date,
        PicName = f.Pic?.Name ?? string.Empty,
        Result = f.Result.ToString().ToUpper(),
        Note = f.Note,
        CreatedAt = f.CreatedAt,
        Photos = (f.Photos ?? new List<AuditFollowUpPhoto>())
            .OrderBy(p => p.Id)
            .Select(p => $"/api/followups/photos/{p.Id}")
            .ToList()
    };
}

public class FollowUpItemResponse
{
    public int Id { get; set; }
    public int TemplateId { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;   // FAIL = masih terbuka, PASS = sudah lulus lewat follow up
    public string? Note { get; set; }                     // catatan audit awal
    public List<string> Photos { get; set; } = new();     // foto audit awal
    public List<FollowUpResponse> FollowUps { get; set; } = new();   // riwayat, terbaru dulu
}

public class FollowUpDetailResponse
{
    public string Id { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public int TenantId { get; set; }
    public string TenantName { get; set; } = string.Empty;
    public int PicId { get; set; }
    public string PicName { get; set; } = string.Empty;
    public bool IsGas { get; set; }
    public int TotalItems { get; set; }
    public int PassCount { get; set; }
    public int FailCount { get; set; }
    public double PassRate { get; set; }
    public List<FollowUpItemResponse> Items { get; set; } = new();
}
