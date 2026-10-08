using HygieneAudit.Application.DTOs;
using System.IO.Compression;
using System.Text;
using static HygieneAudit.Application.Services.FollowUpReportExcelBuilder;

namespace HygieneAudit.Application.Services;

// Export Excel laporan gabungan audit + follow up (3 sheet):
//   Ringkasan | Per Audit (nilai awal vs nilai kini) | Temuan & Follow Up (satu baris per catatan follow up)
// Style: 1 blue header | 2 indigo header | 3 data | 4 alt data | 5 hijau (center) | 6 merah (center)
public static class AuditFollowUpReportExcelBuilder
{
    static readonly string[] SheetNames = { "Ringkasan", "Per Audit", "Temuan & Follow Up" };
    const string WsType = "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml";
    const string Header = "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>";

    public static byte[] Build(AuditFollowUpReportDto report, string filterText)
    {
        var detail = FlattenDetail(report);

        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            ExcelBuilder.Txt(zip, "[Content_Types].xml",        ContentTypes());
            ExcelBuilder.Txt(zip, "_rels/.rels",                ExcelBuilder.RootRels());
            ExcelBuilder.Txt(zip, "xl/workbook.xml",            Workbook(report.Rows.Count, detail.Count));
            ExcelBuilder.Txt(zip, "xl/_rels/workbook.xml.rels", WorkbookRels());
            ExcelBuilder.Txt(zip, "xl/styles.xml",              ExcelBuilder.StyleSheet());
            ExcelBuilder.Txt(zip, "xl/worksheets/sheet1.xml",   SummarySheet(report.Summary, filterText));
            ExcelBuilder.Txt(zip, "xl/worksheets/sheet2.xml",   AuditSheet(report.Rows));
            ExcelBuilder.Txt(zip, "xl/worksheets/sheet3.xml",   DetailSheet(detail));
        }
        return ms.ToArray();
    }

    // Satu baris per catatan follow up; temuan tanpa follow up tetap muncul satu baris.
    sealed class Flat
    {
        public AuditFollowUpReportRow Audit = null!;
        public AuditFollowUpFinding Finding = null!;
        public int Seq;                       // follow up ke-n (0 = belum ada)
        public AuditFollowUpEvent? Event;
    }

    static List<Flat> FlattenDetail(AuditFollowUpReportDto report)
    {
        var list = new List<Flat>();
        foreach (var a in report.Rows)
            foreach (var f in a.Items)
            {
                if (f.FollowUps.Count == 0) list.Add(new Flat { Audit = a, Finding = f, Seq = 0 });
                else
                    for (int k = 0; k < f.FollowUps.Count; k++)
                        list.Add(new Flat { Audit = a, Finding = f, Seq = k + 1, Event = f.FollowUps[k] });
            }
        return list;
    }

    // ── workbook parts ───────────────────────────────────────────────────────────

    static string ContentTypes()
    {
        var sb = new StringBuilder(Header);
        sb.Append("<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">");
        sb.Append("<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>");
        sb.Append("<Default Extension=\"xml\" ContentType=\"application/xml\"/>");
        sb.Append("<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>");
        for (int i = 1; i <= SheetNames.Length; i++)
            sb.Append($"<Override PartName=\"/xl/worksheets/sheet{i}.xml\" ContentType=\"{WsType}\"/>");
        sb.Append("<Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/>");
        sb.Append("</Types>");
        return sb.ToString();
    }

    static string Workbook(int auditRows, int detailRows)
    {
        var sb = new StringBuilder(Header);
        sb.Append("<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><sheets>");
        for (int i = 0; i < SheetNames.Length; i++)
            sb.Append($"<sheet name=\"{ExcelBuilder.Esc(SheetNames[i])}\" sheetId=\"{i + 1}\" r:id=\"rId{i + 1}\"/>");
        sb.Append("</sheets><definedNames>");
        sb.Append($"<definedName name=\"_xlnm._FilterDatabase\" localSheetId=\"1\" hidden=\"1\">'Per Audit'!$A$1:${Col(AuditCols - 1)}${Math.Max(1, auditRows + 1)}</definedName>");
        sb.Append($"<definedName name=\"_xlnm._FilterDatabase\" localSheetId=\"2\" hidden=\"1\">'Temuan &amp; Follow Up'!$A$1:${Col(DetailCols - 1)}${Math.Max(1, detailRows + 1)}</definedName>");
        sb.Append("</definedNames></workbook>");
        return sb.ToString();
    }

    static string WorkbookRels()
    {
        var sb = new StringBuilder(Header);
        sb.Append("<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">");
        for (int i = 1; i <= SheetNames.Length; i++)
            sb.Append($"<Relationship Id=\"rId{i}\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet{i}.xml\"/>");
        sb.Append($"<Relationship Id=\"rId{SheetNames.Length + 1}\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/>");
        sb.Append("</Relationships>");
        return sb.ToString();
    }

    // ── sheet 1: ringkasan ───────────────────────────────────────────────────────

    static string SummarySheet(AuditFollowUpReportSummary s, string filterText)
    {
        var sb = new StringBuilder(2048);
        sb.Append(Header);
        sb.Append("<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">");
        sb.Append("<cols><col min=\"1\" max=\"1\" width=\"42\" customWidth=\"1\"/><col min=\"2\" max=\"2\" width=\"18\" customWidth=\"1\"/></cols><sheetData>");

        int r = 1;
        sb.Append($"<row r=\"{r}\" ht=\"22\" customHeight=\"1\">").Append(Tc(0, r, 2, $"Laporan Audit & Follow Up Hygiene - Dicetak {DateTime.Now:dd/MM/yyyy HH:mm}")).Append(Tc(1, r, 2, null)).Append("</row>"); r++;
        sb.Append($"<row r=\"{r}\" ht=\"32\" customHeight=\"1\">").Append(Tc(0, r, 3, string.IsNullOrWhiteSpace(filterText) ? "Filter: -" : "Filter: " + filterText)).Append(Tc(1, r, 3, null)).Append("</row>"); r++;
        sb.Append($"<row r=\"{r}\" ht=\"20\" customHeight=\"1\">").Append(Tc(0, r, 1, "Indikator")).Append(Tc(1, r, 1, "Nilai")).Append("</row>"); r++;

        var items = new (string Label, object Value)[]
        {
            ("Jumlah Audit Selesai",                      s.Audits),
            ("Audit Tanpa Temuan (100% sejak awal)",      s.CleanAudits),
            ("Audit Dengan Temuan",                       s.AuditsWithFindings),
            ("Total Temuan",                              s.Findings),
            ("Temuan Selesai (Pass)",                     s.Resolved),
            ("Temuan Belum Selesai - dalam target",       s.Open),
            ("Temuan Belum Selesai - lewat target",       s.Overdue),
            ("Rata-rata Nilai Awal Audit (%)",            s.AverageInitialRate),
            ("Rata-rata Nilai Setelah Follow Up (%)",     s.AverageCurrentRate),
        };
        bool alt = false;
        foreach (var (label, value) in items)
        {
            int st = alt ? 4 : 3;
            sb.Append($"<row r=\"{r}\">").Append(Tc(0, r, st, label)).Append(Nc(1, r, st, value)).Append("</row>");
            r++; alt = !alt;
        }
        sb.Append("</sheetData><mergeCells count=\"2\"><mergeCell ref=\"A1:B1\"/><mergeCell ref=\"A2:B2\"/></mergeCells></worksheet>");
        return sb.ToString();
    }

    // ── sheet 2: per audit ───────────────────────────────────────────────────────

    const int AuditCols = 14;

    static string AuditSheet(List<AuditFollowUpReportRow> rows)
    {
        var widths = new[] { 5, 24, 8, 12, 18, 9, 11, 11, 10, 9, 9, 9, 13, 18 };
        var headers = new[]
        {
            "No", "Tenant", "Gas", "Tgl Audit", "PIC Audit", "Item Dinilai", "Nilai Awal (%)", "Nilai Kini (%)",
            "Temuan", "Selesai", "Terbuka", "Terlambat", "Follow Up Terakhir", "Status"
        };

        var sb = new StringBuilder(8192);
        sb.Append(Header);
        sb.Append("<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">");
        sb.Append("<sheetViews><sheetView workbookViewId=\"0\"><pane ySplit=\"1\" topLeftCell=\"A2\" activePane=\"bottomLeft\" state=\"frozen\"/></sheetView></sheetViews>");
        sb.Append("<cols>");
        for (int i = 0; i < widths.Length; i++) sb.Append($"<col min=\"{i + 1}\" max=\"{i + 1}\" width=\"{widths[i]}\" customWidth=\"1\"/>");
        sb.Append("</cols><sheetData>");

        int r = 1;
        sb.Append($"<row r=\"{r}\" ht=\"32\" customHeight=\"1\">");
        for (int c = 0; c < headers.Length; c++) sb.Append(Tc(c, r, 1, headers[c]));
        sb.Append("</row>"); r++;

        bool alt = false;
        foreach (var x in rows)
        {
            int s = alt ? 4 : 3;
            int sCur = x.TotalItems == 0 ? s : x.CurrentRate >= 100 ? 5 : 6;
            int sInit = x.TotalItems == 0 ? s : x.InitialRate >= 100 ? 5 : 6;
            int sStatus = x.Status == "CLEAN" || x.Status == "RESOLVED" ? 5 : x.Status == "OVERDUE" ? 6 : s;
            var statusText = x.Status switch
            {
                "CLEAN"    => "Lulus (tanpa temuan)",
                "RESOLVED" => "Semua temuan selesai",
                "OVERDUE"  => $"Terlambat ({x.Overdue} temuan)",
                _          => $"Berjalan ({x.Open} temuan)"
            };

            sb.Append($"<row r=\"{r}\">");
            sb.Append(Nc(0, r, s, x.No));
            sb.Append(Tc(1, r, s, x.TenantName));
            sb.Append(Tc(2, r, s, x.IsGas ? "Ya" : "Tidak"));
            sb.Append(Tc(3, r, s, D(x.AuditDate)));
            sb.Append(Tc(4, r, s, x.PicName));
            sb.Append(Nc(5, r, s, x.TotalItems));
            sb.Append(Nc(6, r, sInit, x.InitialRate));
            sb.Append(Nc(7, r, sCur, x.CurrentRate));
            sb.Append(Nc(8, r, s, x.Findings));
            sb.Append(Nc(9, r, s, x.Resolved));
            sb.Append(Nc(10, r, s, x.Open));
            sb.Append(Nc(11, r, s, x.Overdue));
            sb.Append(Tc(12, r, s, D(x.LastFollowUpDate)));
            sb.Append(Tc(13, r, sStatus, statusText));
            sb.Append("</row>");
            r++; alt = !alt;
        }

        sb.Append("</sheetData>");
        sb.Append($"<autoFilter ref=\"A1:{Col(AuditCols - 1)}{Math.Max(1, r - 1)}\"/></worksheet>");
        return sb.ToString();
    }

    // ── sheet 3: temuan + riwayat follow up ──────────────────────────────────────

    const int DetailCols = 16;

    static string DetailSheet(List<Flat> rows)
    {
        var widths = new[] { 5, 24, 12, 18, 11, 11, 20, 30, 30, 12, 20, 7, 12, 18, 9, 34 };
        var headers = new[]
        {
            "No", "Tenant", "Tgl Audit", "PIC Audit", "Nilai Awal (%)", "Nilai Kini (%)", "Kategori", "Item Checklist", "Temuan",
            "Target Follow Up", "Status Temuan", "FU ke-", "Tgl Follow Up", "Oleh", "Hasil", "Catatan Follow Up"
        };

        var sb = new StringBuilder(16384);
        sb.Append(Header);
        sb.Append("<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">");
        sb.Append("<sheetViews><sheetView workbookViewId=\"0\"><pane ySplit=\"1\" topLeftCell=\"A2\" activePane=\"bottomLeft\" state=\"frozen\"/></sheetView></sheetViews>");
        sb.Append("<cols>");
        for (int i = 0; i < widths.Length; i++) sb.Append($"<col min=\"{i + 1}\" max=\"{i + 1}\" width=\"{widths[i]}\" customWidth=\"1\"/>");
        sb.Append("</cols><sheetData>");

        int r = 1;
        sb.Append($"<row r=\"{r}\" ht=\"32\" customHeight=\"1\">");
        for (int c = 0; c < headers.Length; c++) sb.Append(Tc(c, r, 1, headers[c]));
        sb.Append("</row>"); r++;

        bool alt = false;
        int no = 1;
        foreach (var x in rows)
        {
            int s = alt ? 4 : 3;
            var f = x.Finding;
            int sFind = f.Status == "RESOLVED" ? 5 : f.Status == "OVERDUE" ? 6 : s;
            int sRes = x.Event == null ? s : x.Event.Result == "PASS" ? 5 : 6;
            var findStatus = f.Status switch
            {
                "RESOLVED" => f.ResolvedLate ? "Selesai (lewat target)" : "Selesai",
                "OVERDUE"  => $"Terlambat {f.DaysOverdue} hari",
                _          => "Terbuka"
            };

            sb.Append($"<row r=\"{r}\">");
            sb.Append(Nc(0, r, s, no++));
            sb.Append(Tc(1, r, s, x.Audit.TenantName));
            sb.Append(Tc(2, r, s, D(x.Audit.AuditDate)));
            sb.Append(Tc(3, r, s, x.Audit.PicName));
            sb.Append(Nc(4, r, s, x.Audit.InitialRate));
            sb.Append(Nc(5, r, s, x.Audit.CurrentRate));
            sb.Append(Tc(6, r, s, f.Category));
            sb.Append(Tc(7, r, s, f.ItemName));
            sb.Append(Tc(8, r, s, f.Finding));
            sb.Append(Tc(9, r, s, f.TargetDate.HasValue ? D(f.TargetDate) : "-"));
            sb.Append(Tc(10, r, sFind, findStatus));
            if (x.Seq > 0) sb.Append(Nc(11, r, s, x.Seq)); else sb.Append(Tc(11, r, s, null));
            sb.Append(Tc(12, r, s, x.Event != null ? D(x.Event.Date) : ""));
            sb.Append(Tc(13, r, s, x.Event?.By));
            sb.Append(Tc(14, r, sRes, x.Event?.Result));
            sb.Append(Tc(15, r, s, x.Event != null ? x.Event.Note : "Belum ada follow up"));
            sb.Append("</row>");
            r++; alt = !alt;
        }

        sb.Append("</sheetData>");
        sb.Append($"<autoFilter ref=\"A1:{Col(DetailCols - 1)}{Math.Max(1, r - 1)}\"/></worksheet>");
        return sb.ToString();
    }
}
