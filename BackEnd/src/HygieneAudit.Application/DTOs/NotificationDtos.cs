namespace HygieneAudit.Application.DTOs;

// Password SMTP tidak pernah dikirim ke UI; HasPassword hanya menandai sudah tersimpan atau belum.
public class NotificationSettingsResponse
{
    public bool    Enabled        { get; set; }
    public string  SmtpHost       { get; set; } = string.Empty;
    public int     SmtpPort       { get; set; }
    public bool    UseSsl         { get; set; }
    public string? SmtpUsername   { get; set; }
    public bool    HasPassword    { get; set; }
    public string  FromAddress    { get; set; } = string.Empty;
    public string? FromName       { get; set; }
    public string  SendTime       { get; set; } = "08:00";
    public bool    IncludeOverdue { get; set; }
    public string? BaseUrl        { get; set; }
    public DateTime? LastRunAt    { get; set; }
    public string? LastRunMessage { get; set; }
    public int     RecipientCount { get; set; }
}

public class SaveNotificationSettingsRequest
{
    public bool    Enabled        { get; set; }
    public string? SmtpHost       { get; set; }
    public int     SmtpPort       { get; set; } = 587;
    public bool    UseSsl         { get; set; } = true;
    public string? SmtpUsername   { get; set; }
    public string? SmtpPassword   { get; set; }   // kosong/null = pertahankan password tersimpan
    public bool    ClearPassword  { get; set; }   // true = hapus password tersimpan
    public string? FromAddress    { get; set; }
    public string? FromName       { get; set; }
    public string? SendTime       { get; set; }
    public bool    IncludeOverdue { get; set; } = true;
    public string? BaseUrl        { get; set; }
}

public class SendTestEmailRequest
{
    public string? To { get; set; }
}

public class NotificationRunResult
{
    public int    DueItems   { get; set; }
    public int    Recipients { get; set; }
    public int    Sent       { get; set; }
    public int    Failed     { get; set; }
    public string Message    { get; set; } = string.Empty;
}
