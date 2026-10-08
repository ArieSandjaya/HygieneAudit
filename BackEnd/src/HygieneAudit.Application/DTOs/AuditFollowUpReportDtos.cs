namespace HygieneAudit.Application.DTOs;

// Laporan gabungan: hasil audit (nilai awal) dihubungkan langsung dengan hasil follow up (nilai kini)
// per audit selesai, lengkap dengan temuan dan riwayat follow up tiap temuan.
public class AuditFollowUpReportDto
{
    public List<AuditFollowUpReportRow> Rows { get; set; } = new();
    public AuditFollowUpReportSummary Summary { get; set; } = new();
}

public class AuditFollowUpReportRow
{
    public int No { get; set; }
    public string AuditId { get; set; } = string.Empty;
    public int TenantId { get; set; }
    public string TenantName { get; set; } = string.Empty;
    public bool IsGas { get; set; }
    public DateTime AuditDate { get; set; }
    public string PicName { get; set; } = string.Empty;

    public int TotalItems { get; set; }          // item mandatori (yang dinilai)
    public int Findings { get; set; }            // item yang Fail saat audit
    public double InitialRate { get; set; }      // persen 0-100, sebelum follow up
    public double CurrentRate { get; set; }      // persen 0-100, setelah follow up
    public int Resolved { get; set; }
    public int Open { get; set; }                // belum selesai, belum lewat target
    public int Overdue { get; set; }             // belum selesai, lewat target
    public int FollowUpCount { get; set; }
    public DateTime? LastFollowUpDate { get; set; }
    public string Status { get; set; } = string.Empty;   // CLEAN | RESOLVED | OPEN | OVERDUE

    public List<AuditFollowUpFinding> Items { get; set; } = new();
}

public class AuditFollowUpFinding
{
    public string Category { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public string Finding { get; set; } = string.Empty;
    public DateTime? TargetDate { get; set; }
    public string Status { get; set; } = string.Empty;   // OPEN | OVERDUE | RESOLVED
    public int DaysOverdue { get; set; }
    public bool ResolvedLate { get; set; }
    public List<AuditFollowUpEvent> FollowUps { get; set; } = new();
}

public class AuditFollowUpEvent
{
    public DateTime Date { get; set; }
    public string By { get; set; } = string.Empty;
    public string Result { get; set; } = string.Empty;   // PASS | FAIL
    public string Note { get; set; } = string.Empty;
}

public class AuditFollowUpReportSummary
{
    public int Audits { get; set; }
    public int CleanAudits { get; set; }         // tanpa temuan
    public int AuditsWithFindings { get; set; }
    public int Findings { get; set; }
    public int Resolved { get; set; }
    public int Open { get; set; }
    public int Overdue { get; set; }
    public double AverageInitialRate { get; set; }   // rata-rata nilai per audit (persen)
    public double AverageCurrentRate { get; set; }
}
