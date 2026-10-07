using System.Globalization;
using System.Net;
using System.Text;

namespace HygieneAudit.Application.Services;

public class DueFollowUp
{
    public string   AuditId    { get; set; } = string.Empty;
    public string   TenantName { get; set; } = string.Empty;
    public string   Category   { get; set; } = string.Empty;
    public string   ItemName   { get; set; } = string.Empty;
    public string?  Finding    { get; set; }
    public DateTime TargetDate { get; set; }
    public int      DaysOverdue { get; set; }   // 0 = jatuh tempo hari ini
}

// Membangun isi email pengingat follow up (HTML sederhana yang aman dibuka di klien email mana pun).
public static class FollowUpReminderEmail
{
    private static readonly CultureInfo Id = new CultureInfo("id-ID");

    public static string Subject(IReadOnlyList<DueFollowUp> items, DateTime today)
    {
        var overdue = items.Count(i => i.DaysOverdue > 0);
        var s = $"[Hygiene Audit] {items.Count} temuan perlu follow up ({today.ToString("dd MMM yyyy", Id)})";
        return overdue > 0 ? s + $" - {overdue} terlambat" : s;
    }

    public static string Body(string recipientName, IReadOnlyList<DueFollowUp> items, DateTime today, string? baseUrl)
    {
        var sb = new StringBuilder();
        sb.Append("<div style=\"font-family:Segoe UI,Arial,sans-serif;font-size:14px;color:#222;\">");
        sb.Append("<p>Halo ").Append(E(recipientName)).Append(",</p>");
        sb.Append("<p>Per <b>").Append(today.ToString("dddd, dd MMMM yyyy", Id)).Append("</b> terdapat <b>")
          .Append(items.Count).Append("</b> temuan audit kebersihan yang tanggal follow up-nya sudah tiba dan belum berstatus Pass.</p>");

        foreach (var g in items.GroupBy(i => i.TenantName).OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase))
        {
            sb.Append("<h4 style=\"margin:18px 0 6px;\">").Append(E(g.Key)).Append("</h4>");
            sb.Append("<table style=\"border-collapse:collapse;width:100%;\">");
            sb.Append("<tr style=\"background:#f1f5f9;text-align:left;\"><th style=\"padding:6px 8px;border:1px solid #e2e8f0;\">Kategori</th>")
              .Append("<th style=\"padding:6px 8px;border:1px solid #e2e8f0;\">Item</th>")
              .Append("<th style=\"padding:6px 8px;border:1px solid #e2e8f0;\">Temuan</th>")
              .Append("<th style=\"padding:6px 8px;border:1px solid #e2e8f0;\">Target</th></tr>");
            foreach (var i in g.OrderByDescending(x => x.DaysOverdue).ThenBy(x => x.Category).ThenBy(x => x.ItemName))
            {
                var status = i.DaysOverdue > 0
                    ? $"<span style=\"color:#b91c1c;font-weight:600;\">Terlambat {i.DaysOverdue} hari</span>"
                    : "<span style=\"color:#b45309;font-weight:600;\">Hari ini</span>";
                var link = BuildLink(baseUrl, i.AuditId);
                var item = link == null ? E(i.ItemName) : $"<a href=\"{E(link)}\">{E(i.ItemName)}</a>";
                sb.Append("<tr><td style=\"padding:6px 8px;border:1px solid #e2e8f0;\">").Append(E(i.Category))
                  .Append("</td><td style=\"padding:6px 8px;border:1px solid #e2e8f0;\">").Append(item)
                  .Append("</td><td style=\"padding:6px 8px;border:1px solid #e2e8f0;\">").Append(E(i.Finding ?? "-"))
                  .Append("</td><td style=\"padding:6px 8px;border:1px solid #e2e8f0;white-space:nowrap;\">")
                  .Append(i.TargetDate.ToString("dd MMM yyyy", Id)).Append("<br/>").Append(status).Append("</td></tr>");
            }
            sb.Append("</table>");
        }

        sb.Append("<p style=\"margin-top:18px;color:#64748b;font-size:12px;\">Email ini dikirim otomatis oleh sistem Hygiene Audit. ")
          .Append("Catat hasil follow up di menu Follow Up; temuan hilang dari pengingat setelah berstatus Pass.</p></div>");
        return sb.ToString();
    }

    public static string Text(string recipientName, IReadOnlyList<DueFollowUp> items, DateTime today, string? baseUrl)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Halo " + recipientName + ",").AppendLine();
        sb.AppendLine($"Per {today.ToString("dddd, dd MMMM yyyy", Id)} terdapat {items.Count} temuan audit kebersihan yang tanggal follow up-nya sudah tiba dan belum berstatus Pass.").AppendLine();
        foreach (var g in items.GroupBy(i => i.TenantName).OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase))
        {
            sb.AppendLine(g.Key);
            foreach (var i in g.OrderByDescending(x => x.DaysOverdue).ThenBy(x => x.Category).ThenBy(x => x.ItemName))
            {
                var st = i.DaysOverdue > 0 ? $"terlambat {i.DaysOverdue} hari" : "hari ini";
                sb.AppendLine($" - [{i.Category}] {i.ItemName}: {i.Finding ?? "-"} (target {i.TargetDate.ToString("dd MMM yyyy", Id)}, {st})");
                var link = BuildLink(baseUrl, i.AuditId);
                if (link != null) sb.AppendLine("   " + link);
            }
            sb.AppendLine();
        }
        sb.AppendLine("Email ini dikirim otomatis oleh sistem Hygiene Audit.");
        return sb.ToString();
    }

    private static string? BuildLink(string? baseUrl, string auditId)
    {
        if (string.IsNullOrWhiteSpace(baseUrl)) return null;
        return baseUrl!.TrimEnd('/') + "/FollowUps/Detail/" + Uri.EscapeDataString(auditId);
    }

    private static string E(string s) => WebUtility.HtmlEncode(s ?? string.Empty);
}
