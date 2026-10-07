using HygieneAudit.Application.DTOs;

namespace HygieneAudit.Application.Services;

public interface INotificationService
{
    Task<NotificationSettingsResponse> GetSettingsAsync();
    Task<NotificationSettingsResponse> SaveSettingsAsync(SaveNotificationSettingsRequest req);
    Task SendTestEmailAsync(string toAddress);

    // Kirim pengingat untuk temuan yang tanggal follow up-nya sudah tiba, tanpa memeriksa jam/tanggal terakhir kirim.
    Task<NotificationRunResult> RunNowAsync(DateTime today);

    // Dipanggil penjadwal tiap menit: menjalankan pengiriman sekali per hari bila jam kirim sudah lewat.
    // Mengembalikan null bila belum waktunya / dinonaktifkan.
    Task<NotificationRunResult?> RunScheduledAsync(DateTime now);
}

// ── Abstraksi infrastruktur (diimplementasikan di WebApps; dipalsukan saat uji) ──────────

public class SmtpConfig
{
    public string  Host     { get; set; } = string.Empty;
    public int     Port     { get; set; }
    public bool    UseSsl   { get; set; }
    public string? Username { get; set; }
    public string? Password { get; set; }
}

public class EmailMessage
{
    public string  FromAddress { get; set; } = string.Empty;
    public string? FromName    { get; set; }
    public string  To          { get; set; } = string.Empty;
    public string  Subject     { get; set; } = string.Empty;
    public string  HtmlBody    { get; set; } = string.Empty;
    public string? TextBody    { get; set; }   // versi teks polos (mengurangi risiko masuk spam)
}

public interface IEmailSender
{
    // Melempar exception bila gagal.
    Task SendAsync(SmtpConfig config, EmailMessage message);
}

public interface ISecretProtector
{
    string Protect(string plain);
    string? Unprotect(string protectedValue);
}
