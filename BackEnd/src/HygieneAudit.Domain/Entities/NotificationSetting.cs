namespace HygieneAudit.Domain.Entities;

// Pengaturan notifikasi email follow up. Hanya satu baris (Id = 1), diatur Admin lewat UI.
public class NotificationSetting
{
    public const int SingletonId = 1;

    public int Id { get; set; } = SingletonId;

    // Pengiriman otomatis harian aktif/tidak.
    public bool Enabled { get; set; }

    public string SmtpHost { get; set; } = string.Empty;
    public int SmtpPort { get; set; } = 587;
    public bool UseSsl { get; set; } = true;
    public string? SmtpUsername { get; set; }
    // Password SMTP disimpan terenkripsi (tidak pernah dikirim balik ke UI).
    public string? SmtpPasswordProtected { get; set; }
    public string FromAddress { get; set; } = string.Empty;
    public string? FromName { get; set; }

    // Jam kirim harian, format "HH:mm" (jam server).
    public string SendTime { get; set; } = "08:00";
    // true: sertakan temuan yang sudah lewat tanggal follow up dan belum Pass; false: hanya yang jatuh tempo hari ini.
    public bool IncludeOverdue { get; set; } = true;
    // Alamat dasar aplikasi untuk tautan di email (opsional), mis. https://hygiene.perusahaan.com
    public string? BaseUrl { get; set; }

    // Tanggal terakhir pengiriman terjadwal dijalankan (mencegah kirim ganda di hari yang sama).
    public DateTime? LastRunDate { get; set; }
    public DateTime? LastRunAt { get; set; }
    public string? LastRunMessage { get; set; }
}
