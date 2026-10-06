using System;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;
using HygieneAudit.Application.Services;

namespace WebApps.Helpers
{
    // Pengirim email lewat SMTP (System.Net.Mail). Konfigurasi diambil dari pengaturan yang diatur Admin di UI.
    public class SmtpEmailSender : IEmailSender
    {
        public async Task SendAsync(SmtpConfig config, EmailMessage message)
        {
            using (var mail = new MailMessage())
            using (var client = new SmtpClient(config.Host, config.Port))
            {
                mail.From = string.IsNullOrWhiteSpace(message.FromName)
                    ? new MailAddress(message.FromAddress)
                    : new MailAddress(message.FromAddress, message.FromName, Encoding.UTF8);
                mail.To.Add(new MailAddress(message.To));
                mail.Subject = message.Subject;
                mail.SubjectEncoding = Encoding.UTF8;
                mail.Body = message.HtmlBody;
                mail.BodyEncoding = Encoding.UTF8;
                mail.IsBodyHtml = true;

                client.EnableSsl = config.UseSsl;
                client.DeliveryMethod = SmtpDeliveryMethod.Network;
                client.Timeout = 30000;
                if (!string.IsNullOrEmpty(config.Username))
                    client.Credentials = new NetworkCredential(config.Username, config.Password ?? string.Empty);
                else
                    client.UseDefaultCredentials = false;

                await client.SendMailAsync(mail).ConfigureAwait(false);
            }
        }
    }
}
