namespace HygieneAudit.Domain.DTOs;

public class ExcelReportDto
{
    public List<ExcelReportRow> Rows { get; set; } = new();
    public ExcelReportSummary Summary { get; set; } = new();
}

public class ExcelReportRow
{
    public int No { get; set; }
    public string TenantName { get; set; } = string.Empty;
    public bool UsesGas { get; set; }
    public DateTime Date { get; set; }
    public string PicName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int TotalItems { get; set; }
    public int PassItems { get; set; }
    public int FailItems { get; set; }
    public double PassRate { get; set; }
    public string FailNotes { get; set; } = string.Empty;
}

public class ExcelReportSummary
{
    public double AveragePassRate { get; set; }
    public int TotalItems { get; set; }
    public int TotalPass { get; set; }
    public int TotalFail { get; set; }
    public int TenantCount { get; set; }
}
