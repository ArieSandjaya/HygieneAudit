using HygieneAudit.Application.DTOs;
using System.IO.Compression;
using System.Text;

namespace HygieneAudit.Application.Services;

// Export Excel untuk laporan follow up (2 sheet: Ringkasan dan Detail Follow Up).
// Memakai helper dan stylesheet bersama dari ExcelBuilder:
// 1 blue header | 2 indigo header | 3 data | 4 alt data | 5 hijau (center) | 6 merah (center)
public static class FollowUpReportExcelBuilder
{
    const int DetailColumns = 15;   // A..O

    public static byte[] Build(FollowUpReportDto report, string filterText)
    {
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            ExcelBuilder.Txt(zip, "[Content_Types].xml",        ContentTypes());
            ExcelBuilder.Txt(zip, "_rels/.rels",                ExcelBuilder.RootRels());
            ExcelBuilder.Txt(zip, "xl/workbook.xml",            Workbook(report.Rows.Count));
            ExcelBuilder.Txt(zip, "xl/_rels/workbook.xml.rels", WorkbookRels());
            ExcelBuilder.Txt(zip, "xl/styles.xml",              ExcelBuilder.StyleSheet());
            ExcelBuilder.Txt(zip, "xl/worksheets/sheet1.xml",   SummarySheet(report.Summary, filterText));
            ExcelBuilder.Txt(zip, "xl/worksheets/sheet2.xml",   DetailSheet(report.Rows));
        }
        return ms.ToArray();
    }

    // ── cell helpers (kolom A..Z) ─────────────────────────────────────────────────

    internal static string Col(int i) => ((char)('A' + i)).ToString();

    // Hapus karakter kontrol yang tidak valid di XML 1.0 (mis. hasil copy-paste).
    internal static string Clean(string? s)
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;
        var sb = new StringBuilder(s.Length);
        foreach (var ch in s)
            if (ch == '\t' || ch == '\n' || ch == '\r' || ch >= ' ') sb.Append(ch);
        return sb.ToString();
    }

    internal static string Tc(int col, int row, int style, string? text)
    {
        var clean = Clean(text);
        var r = Col(col) + row;
        return clean.Length == 0
            ? $"<c r=\"{r}\" s=\"{style}\"/>"
            : $"<c r=\"{r}\" s=\"{style}\" t=\"inlineStr\"><is><t xml:space=\"preserve\">{ExcelBuilder.Esc(clean)}</t></is></c>";
    }

    internal static string Nc(int col, int row, int style, object n)
        => $"<c r=\"{Col(col)}{row}\" s=\"{style}\"><v>{Convert.ToString(n, System.Globalization.CultureInfo.InvariantCulture)}</v></c>";

    internal static string D(DateTime? d) => d.HasValue ? d.Value.ToString("dd/MM/yyyy") : "";

    // ── workbook parts ────────────────────────────────────────────────────────────

    static string ContentTypes() =>
        "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
        "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">" +
        "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>" +
        "<Default Extension=\"xml\" ContentType=\"application/xml\"/>" +
        "<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>" +
        "<Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>" +
        "<Override PartName=\"/xl/worksheets/sheet2.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>" +
        "<Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/>" +
        "</Types>";

    static string Workbook(int rowCount)
    {
        var last = Math.Max(1, rowCount + 1);
        return "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">" +
            "<sheets>" +
            "<sheet name=\"Ringkasan\" sheetId=\"1\" r:id=\"rId1\"/>" +
            "<sheet name=\"Detail Follow Up\" sheetId=\"2\" r:id=\"rId2\"/>" +
            "</sheets>" +
            $"<definedNames><definedName name=\"_xlnm._FilterDatabase\" localSheetId=\"1\" hidden=\"1\">'Detail Follow Up'!$A$1:${Col(DetailColumns - 1)}${last}</definedName></definedNames>" +
            "</workbook>";
    }

    static string WorkbookRels() =>
        "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
        "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
        "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/>" +
        "<Relationship Id=\"rId2\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet2.xml\"/>" +
        "<Relationship Id=\"rId3\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/>" +
        "</Relationships>";

    // ── sheet 1: ringkasan ────────────────────────────────────────────────────────

    static string SummarySheet(FollowUpReportSummary s, string filterText)
    {
        var sb = new StringBuilder(2048);
        sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
        sb.Append("<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">");
        sb.Append("<cols><col min=\"1\" max=\"1\" width=\"40\" customWidth=\"1\"/><col min=\"2\" max=\"2\" width=\"18\" customWidth=\"1\"/></cols>");
        sb.Append("<sheetData>");

        int r = 1;
        sb.Append($"<row r=\"{r}\" ht=\"22\" customHeight=\"1\">");
        sb.Append(Tc(0, r, 2, $"Laporan Follow Up Hygiene - Dicetak {DateTime.Now:dd/MM/yyyy HH:mm}"));
        sb.Append(Tc(1, r, 2, null));
        sb.Append("</row>"); r++;

        sb.Append($"<row r=\"{r}\" ht=\"32\" customHeight=\"1\">");
        sb.Append(Tc(0, r, 3, string.IsNullOrWhiteSpace(filterText) ? "Filter: -" : "Filter: " + filterText));
        sb.Append(Tc(1, r, 3, null));
        sb.Append("</row>"); r++;

        sb.Append($"<row r=\"{r}\" ht=\"20\" customHeight=\"1\">");
        sb.Append(Tc(0, r, 1, "Indikator"));
        sb.Append(Tc(1, r, 1, "Nilai"));
        sb.Append("</row>"); r++;

        var items = new (string Label, object Value)[]
        {
            ("Total Temuan",                          s.TotalFindings),
            ("Selesai (Pass)",                        s.Resolved),
            ("Belum Selesai - dalam target / tanpa target", s.Open),
            ("Belum Selesai - lewat target",          s.Overdue),
            ("Selesai tetapi melewati target",        s.ResolvedLate),
            ("Tingkat Penyelesaian (%)",              s.ResolutionRate),
            ("Rata-rata Hari Penyelesaian",           s.AverageDaysToResolve),
            ("Jumlah Tenant",                         s.TenantCount),
            ("Total Follow Up Dicatat",               s.TotalFollowUps),
        };
        bool alt = false;
        foreach (var (label, value) in items)
        {
            int st = alt ? 4 : 3;
            sb.Append($"<row r=\"{r}\">");
            sb.Append(Tc(0, r, st, label));
            sb.Append(Nc(1, r, st, value));
            sb.Append("</row>");
            r++; alt = !alt;
        }

        sb.Append("</sheetData>");
        sb.Append("<mergeCells count=\"2\"><mergeCell ref=\"A1:B1\"/><mergeCell ref=\"A2:B2\"/></mergeCells>");
        sb.Append("</worksheet>");
        return sb.ToString();
    }

    // ── sheet 2: detail ───────────────────────────────────────────────────────────

    static string DetailSheet(List<FollowUpReportRow> rows)
    {
        var widths = new[] { 5, 22, 12, 20, 30, 32, 12, 13, 9, 12, 16, 8, 32, 12, 10 };
        var headers = new[]
        {
            "No", "Tenant", "Tgl Audit", "Kategori", "Item Checklist", "Temuan", "Target Follow Up", "Status",
            "Jml Follow Up", "Follow Up Terakhir", "Oleh", "Hasil", "Catatan Follow Up", "Tgl Selesai", "Durasi (hari)"
        };

        var sb = new StringBuilder(8192);
        sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
        sb.Append("<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">");
        sb.Append("<sheetViews><sheetView workbookViewId=\"0\"><pane ySplit=\"1\" topLeftCell=\"A2\" activePane=\"bottomLeft\" state=\"frozen\"/></sheetView></sheetViews>");
        sb.Append("<cols>");
        for (int i = 0; i < widths.Length; i++)
            sb.Append($"<col min=\"{i + 1}\" max=\"{i + 1}\" width=\"{widths[i]}\" customWidth=\"1\"/>");
        sb.Append("</cols>");
        sb.Append("<sheetData>");

        int r = 1;
        sb.Append($"<row r=\"{r}\" ht=\"32\" customHeight=\"1\">");
        for (int c = 0; c < headers.Length; c++) sb.Append(Tc(c, r, 1, headers[c]));
        sb.Append("</row>"); r++;

        bool alt = false;
        foreach (var x in rows)
        {
            int s = alt ? 4 : 3;
            int sStatus = x.Status == "RESOLVED" ? 5 : x.Status == "OVERDUE" ? 6 : s;
            int sResult = x.LastFollowUpResult == "PASS" ? 5 : x.LastFollowUpResult == "FAIL" ? 6 : s;

            var statusText = x.Status switch
            {
                "RESOLVED" => x.ResolvedLate ? "Selesai (lewat target)" : "Selesai",
                "OVERDUE"  => $"Terlambat {x.DaysOverdue} hari",
                _          => "Terbuka"
            };

            sb.Append($"<row r=\"{r}\">");
            sb.Append(Nc(0,  r, s, x.No));
            sb.Append(Tc(1,  r, s, x.TenantName));
            sb.Append(Tc(2,  r, s, D(x.AuditDate)));
            sb.Append(Tc(3,  r, s, x.Category));
            sb.Append(Tc(4,  r, s, x.ItemName));
            sb.Append(Tc(5,  r, s, x.Finding));
            sb.Append(Tc(6,  r, s, x.TargetDate.HasValue ? D(x.TargetDate) : "-"));
            sb.Append(Tc(7,  r, sStatus, statusText));
            sb.Append(Nc(8,  r, s, x.FollowUpCount));
            sb.Append(Tc(9,  r, s, D(x.LastFollowUpDate)));
            sb.Append(Tc(10, r, s, x.LastFollowUpBy));
            sb.Append(Tc(11, r, sResult, x.LastFollowUpResult));
            sb.Append(Tc(12, r, s, x.LastFollowUpNote));
            sb.Append(Tc(13, r, s, D(x.ResolvedDate)));
            if (x.DaysToResolve.HasValue) sb.Append(Nc(14, r, s, x.DaysToResolve.Value)); else sb.Append(Tc(14, r, s, null));
            sb.Append("</row>");
            r++; alt = !alt;
        }

        sb.Append("</sheetData>");
        sb.Append($"<autoFilter ref=\"A1:{Col(DetailColumns - 1)}{Math.Max(1, r - 1)}\"/>");
        sb.Append("</worksheet>");
        return sb.ToString();
    }
}
