using System.Globalization;
using System.Net.Mail;
using System.Text.RegularExpressions;
using HygieneAudit.Application.DTOs;
using HygieneAudit.Application.Exceptions;
using HygieneAudit.Domain;
using HygieneAudit.Domain.Entities;
using HygieneAudit.Domain.Interfaces;

namespace HygieneAudit.Application.Services;

public class NotificationService : INotificationService
{
    private const string DefaultAlias = "Hygiene Audit";
    private static readonly Regex TimeRe = new Regex(@"^([01]\d|2[0-3]):[0-5]\d$", RegexOptions.Compiled);

    private readonly IUnitOfWork _uow;
    private readonly IEmailSender _sender;
    private readonly ISecretProtector _protector;

    public NotificationService(IUnitOfWork uow, IEmailSender sender, ISecretProtector protector)
    {
        _uow = uow;
        _sender = sender;
        _protector = protector;
    }

    // ── Pengaturan ───────────────────────────────────────────────────────────

    public async Task<NotificationSettingsResponse> GetSettingsAsync()
    {
        var s = await _uow.NotificationSettings.GetByIdAsync(NotificationSetting.SingletonId) ?? new NotificationSetting();
        return await ToResponseAsync(s);
    }

    public async Task<NotificationSettingsResponse> SaveSettingsAsync(SaveNotificationSettingsRequest req)
    {
        if (req == null) throw new ValidationException("Data pengaturan tidak boleh kosong.");

        var host = (req.SmtpHost ?? string.Empty).Trim();
        var from = (req.FromAddress ?? string.Empty).Trim();
        var time = (req.SendTime ?? string.Empty).Trim();
        var baseUrl = (req.BaseUrl ?? string.Empty).Trim();

        if (!TimeRe.IsMatch(time))
            throw new ValidationException("Jam kirim harus berformat HH:mm (00:00 - 23:59).");
        if (req.SmtpPort < 1 || req.SmtpPort > 65535)
            throw new ValidationException("Port SMTP harus antara 1 dan 65535.");
        if (from.Length > 0 && !IsValidEmail(from))
            throw new ValidationException("Alamat pengirim tidak valid.");
        if (baseUrl.Length > 0 &&
            !(Uri.TryCreate(baseUrl, UriKind.Absolute, out var u) && (u.Scheme == "http" || u.Scheme == "https")))
            throw new ValidationException("Alamat dasar aplikasi harus diawali http:// atau https://.");
        if (req.Enabled && (host.Length == 0 || from.Length == 0))
            throw new ValidationException("Host SMTP dan alamat pengirim wajib diisi untuk mengaktifkan notifikasi.");

        var s = await _uow.NotificationSettings.GetByIdAsync(NotificationSetting.SingletonId);
        var isNew = s == null;
        s ??= new NotificationSetting();

        s.Enabled = req.Enabled;
        s.SmtpHost = host;
        s.SmtpPort = req.SmtpPort;
        s.UseSsl = req.UseSsl;
        s.SmtpUsername = string.IsNullOrWhiteSpace(req.SmtpUsername) ? null : req.SmtpUsername!.Trim();
        s.FromAddress = from;
        s.FromName = string.IsNullOrWhiteSpace(req.FromName) ? DefaultAlias : req.FromName!.Trim();
        s.SendTime = time;
        s.IncludeOverdue = req.IncludeOverdue;
        s.BaseUrl = baseUrl.Length == 0 ? null : baseUrl;

        if (req.ClearPassword) s.SmtpPasswordProtected = null;
        else if (!string.IsNullOrEmpty(req.SmtpPassword)) s.SmtpPasswordProtected = _protector.Protect(req.SmtpPassword!);

        if (isNew) await _uow.NotificationSettings.AddAsync(s);
        else await _uow.NotificationSettings.UpdateAsync(s);
        await _uow.SaveChangesAsync();

        return await ToResponseAsync(s);
    }

    public async Task SendTestEmailAsync(string toAddress)
    {
        toAddress = (toAddress ?? string.Empty).Trim();
        if (!IsValidEmail(toAddress)) throw new ValidationException("Alamat email tujuan tidak valid.");

        var s = await _uow.NotificationSettings.GetByIdAsync(NotificationSetting.SingletonId);
        if (s == null || string.IsNullOrWhiteSpace(s.SmtpHost) || string.IsNullOrWhiteSpace(s.FromAddress))
            throw new ValidationException("Simpan pengaturan SMTP (host dan pengirim) terlebih dahulu.");

        try
        {
            await _sender.SendAsync(ToSmtp(s), new EmailMessage
            {
                FromAddress = s.FromAddress,
                FromName = string.IsNullOrWhiteSpace(s.FromName) ? DefaultAlias : s.FromName,
                To = toAddress,
                Subject = "[Hygiene Audit] Tes notifikasi email",
                HtmlBody = "<p>Ini adalah email uji dari sistem Hygiene Audit. Jika Anda menerima email ini, pengaturan SMTP sudah benar.</p>"
            });
        }
        catch (Exception ex)
        {
            throw new ValidationException("Gagal mengirim email uji: " + Short(ex));
        }
    }

    // ── Pengiriman ───────────────────────────────────────────────────────────

    public async Task<NotificationRunResult?> RunScheduledAsync(DateTime now)
    {
        var s = await _uow.NotificationSettings.GetByIdAsync(NotificationSetting.SingletonId);
        if (s == null || !s.Enabled) return null;
        if (!TimeRe.IsMatch(s.SendTime ?? string.Empty)) return null;

        var sendAt = TimeSpan.ParseExact(s.SendTime, @"hh\:mm", CultureInfo.InvariantCulture);
        if (now.TimeOfDay < sendAt) return null;
        if (s.LastRunDate.HasValue && s.LastRunDate.Value.Date == now.Date) return null;

        // Tandai dulu agar tick berikutnya (atau instance lain) tidak mengirim ganda di hari yang sama.
        s.LastRunDate = now.Date;
        await _uow.NotificationSettings.UpdateAsync(s);
        await _uow.SaveChangesAsync();

        try { return await RunCoreAsync(s, now); }
        catch (Exception ex)
        {
            // Jangan hilang diam-diam: catat penyebabnya agar terlihat di halaman pengaturan.
            s.LastRunAt = DateTime.Now;
            s.LastRunMessage = Truncate("Pengiriman terjadwal gagal: " + Short(ex), 1000);
            try { await _uow.NotificationSettings.UpdateAsync(s); await _uow.SaveChangesAsync(); } catch { }
            return new NotificationRunResult { Failed = 1, Message = s.LastRunMessage };
        }
    }

    public async Task<NotificationRunResult> RunNowAsync(DateTime today)
    {
        var s = await _uow.NotificationSettings.GetByIdAsync(NotificationSetting.SingletonId);
        if (s == null || string.IsNullOrWhiteSpace(s.SmtpHost) || string.IsNullOrWhiteSpace(s.FromAddress))
            throw new ValidationException("Simpan pengaturan SMTP (host dan pengirim) terlebih dahulu.");
        return await RunCoreAsync(s, today);
    }

    private async Task<NotificationRunResult> RunCoreAsync(NotificationSetting s, DateTime now)
    {
        var today = now.Date;
        var result = new NotificationRunResult();
        try
        {
            var stats = new DueStats();
            var due = await GetDueAsync(today, s.IncludeOverdue, stats);
            var recipients = (await _uow.Users.GetAllAsync())
                .Where(u => u.IsActive && u.ReceiveFollowUpNotification && !string.IsNullOrWhiteSpace(u.Email))
                .ToList();

            result.DueItems = due.Count;
            result.Recipients = recipients.Count;

            if (due.Count == 0)
            {
                result.Message = "Tidak ada temuan yang jatuh tempo follow up. " +
                    $"(Audit selesai: {stats.CompletedAudits}; temuan Fail wajib belum Pass: {stats.OpenFails}; " +
                    $"yang punya tanggal follow up: {stats.WithDate}; tanggal paling awal: {(stats.EarliestDate.HasValue ? stats.EarliestDate.Value.ToString("dd MMM yyyy", CultureInfo.InvariantCulture) : "-")}; " +
                    $"hari ini: {today.ToString("dd MMM yyyy", CultureInfo.InvariantCulture)}{(s.IncludeOverdue ? string.Empty : "; hanya yang tepat hari ini")}.)";
            }
            else if (recipients.Count == 0)
            {
                var total = (await _uow.Users.GetAllAsync()).Count();
                result.Message = $"{due.Count} temuan jatuh tempo, tetapi belum ada pengguna penerima notifikasi (dengan email) yang aktif. " +
                                 $"Centang 'Terima notifikasi' dan isi email di menu Pengguna (total pengguna: {total}).";
            }
            else
            {
                var smtp = ToSmtp(s);
                var errors = new List<string>();
                foreach (var r in recipients)
                {
                    try
                    {
                        await _sender.SendAsync(smtp, new EmailMessage
                        {
                            FromAddress = s.FromAddress,
                            FromName = string.IsNullOrWhiteSpace(s.FromName) ? DefaultAlias : s.FromName,
                            To = r.Email!,
                            Subject = FollowUpReminderEmail.Subject(due, today),
                            HtmlBody = FollowUpReminderEmail.Body(r.Name, due, today, s.BaseUrl),
                            TextBody = FollowUpReminderEmail.Text(r.Name, due, today, s.BaseUrl)
                        });
                        result.Sent++;
                    }
                    catch (Exception ex)
                    {
                        result.Failed++;
                        errors.Add($"{r.Email}: {Short(ex)}");
                    }
                }
                result.Message = $"{due.Count} temuan, diserahkan ke server SMTP untuk {result.Sent} dari {recipients.Count} penerima " +
                                 $"({string.Join(", ", recipients.Select(r => r.Email))})." +
                                 (errors.Count > 0 ? " Gagal: " + string.Join("; ", errors) : string.Empty);
            }
        }
        catch (Exception ex)
        {
            result.Message = "Pengiriman gagal: " + Short(ex);
            result.Failed = Math.Max(result.Failed, 1);
        }

        s.LastRunAt = DateTime.Now;
        s.LastRunMessage = Truncate(result.Message, 1000);
        await _uow.NotificationSettings.UpdateAsync(s);
        await _uow.SaveChangesAsync();
        return result;
    }

    // Temuan mandatori berstatus Fail (belum Pass) yang tanggal follow up-nya sudah tiba.
    private async Task<List<DueFollowUp>> GetDueAsync(DateTime today, bool includeOverdue, DueStats stats)
    {
        var audits = await _uow.Audits.GetCompletedForFollowUpReportAsync(null, null, null, null);
        var list = new List<DueFollowUp>();
        foreach (var a in audits)
        {
            stats.CompletedAudits++;
            foreach (var i in AuditScoring.Scored(a.Items))
            {
                if (i.Status != AuditItemStatus.Fail) continue;
                stats.OpenFails++;
                if (!i.FollowUpDate.HasValue) continue;
                stats.WithDate++;
                var target = i.FollowUpDate.Value.Date;
                if (!stats.EarliestDate.HasValue || target < stats.EarliestDate.Value) stats.EarliestDate = target;
                if (target > today) continue;
                var overdue = (today - target).Days;
                if (!includeOverdue && overdue > 0) continue;
                list.Add(new DueFollowUp
                {
                    AuditId = a.Id,
                    TenantName = a.Tenant?.Name ?? string.Empty,
                    Category = i.Category,
                    ItemName = i.Name,
                    Finding = i.Note,
                    TargetDate = target,
                    DaysOverdue = overdue
                });
            }
        }
        return list;
    }

    private class DueStats
    {
        public int CompletedAudits, OpenFails, WithDate;
        public DateTime? EarliestDate;
    }

    // ── Helper ───────────────────────────────────────────────────────────────

    private SmtpConfig ToSmtp(NotificationSetting s) => new SmtpConfig
    {
        Host = s.SmtpHost,
        Port = s.SmtpPort,
        UseSsl = s.UseSsl,
        Username = s.SmtpUsername,
        Password = string.IsNullOrEmpty(s.SmtpPasswordProtected) ? null : _protector.Unprotect(s.SmtpPasswordProtected!)
    };

    private async Task<NotificationSettingsResponse> ToResponseAsync(NotificationSetting s)
    {
        var recipients = (await _uow.Users.GetAllAsync())
            .Count(u => u.IsActive && u.ReceiveFollowUpNotification && !string.IsNullOrWhiteSpace(u.Email));
        return new NotificationSettingsResponse
        {
            Enabled = s.Enabled,
            SmtpHost = s.SmtpHost,
            SmtpPort = s.SmtpPort,
            UseSsl = s.UseSsl,
            SmtpUsername = s.SmtpUsername,
            HasPassword = !string.IsNullOrEmpty(s.SmtpPasswordProtected),
            FromAddress = s.FromAddress,
            FromName = string.IsNullOrWhiteSpace(s.FromName) ? DefaultAlias : s.FromName,
            SendTime = s.SendTime,
            IncludeOverdue = s.IncludeOverdue,
            BaseUrl = s.BaseUrl,
            LastRunAt = s.LastRunAt,
            LastRunMessage = s.LastRunMessage,
            RecipientCount = recipients
        };
    }

    private static bool IsValidEmail(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 256) return false;
        try { return new MailAddress(value).Address == value; }
        catch (FormatException) { return false; }
    }

    private static string Short(Exception ex)
    {
        var e = ex;
        while (e.InnerException != null) e = e.InnerException;
        return Truncate(e.Message, 300);
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s.Substring(0, max);
}
