namespace HygieneAudit.Application.DTOs;

// Laporan follow up: satu baris = satu temuan (item mandatori yang pernah Fail) pada audit yang sudah selesai.
public class FollowUpReportDto
{
    public List<FollowUpReportRow> Rows { get; set; } = new();
    public FollowUpReportSummary Summary { get; set; } = new();
}

public class FollowUpReportRow
{
    public int No { get; set; }
    public string AuditId { get; set; } = string.Empty;
    public int TenantId { get; set; }
    public string TenantName { get; set; } = string.Empty;
    public bool IsGas { get; set; }
    public DateTime AuditDate { get; set; }
    public string AuditPicName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public string Finding { get; set; } = string.Empty;          // catatan temuan saat audit
    public DateTime? TargetDate { get; set; }                     // tanggal rencana follow up
    public string Status { get; set; } = string.Empty;           // OPEN | OVERDUE | RESOLVED
    public int DaysOverdue { get; set; }                          // hanya untuk OVERDUE
    public int FollowUpCount { get; set; }
    public DateTime? LastFollowUpDate { get; set; }
    public string LastFollowUpBy { get; set; } = string.Empty;
    public string LastFollowUpResult { get; set; } = string.Empty;   // PASS | FAIL | kosong bila belum ada
    public string LastFollowUpNote { get; set; } = string.Empty;
    public DateTime? ResolvedDate { get; set; }                   // tanggal follow up yang meluluskan item
    public int? DaysToResolve { get; set; }                       // tanggal audit -> selesai
    public bool ResolvedLate { get; set; }                        // selesai setelah tanggal target
}

public class FollowUpReportSummary
{
    public int TotalFindings { get; set; }
    public int Resolved { get; set; }
    public int Open { get; set; }              // belum selesai, belum lewat target (atau tanpa target)
    public int Overdue { get; set; }           // belum selesai dan lewat target
    public int ResolvedLate { get; set; }
    public double ResolutionRate { get; set; } // persen 0-100
    public double AverageDaysToResolve { get; set; }
    public int TenantCount { get; set; }
    public int TotalFollowUps { get; set; }
}
