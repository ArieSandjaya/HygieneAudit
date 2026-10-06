using System;
using System.Security.Cryptography;
using System.Text;
using HygieneAudit.Application.Services;

namespace WebApps.Helpers
{
    // Mengenkripsi rahasia (password SMTP) dengan Windows DPAPI pada cakupan mesin,
    // sehingga nilai di database tidak terbaca tanpa akses ke server aplikasi.
    public class DpapiSecretProtector : ISecretProtector
    {
        private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("HygieneAudit.SmtpPassword.v1");

        public string Protect(string plain)
        {
            var data = ProtectedData.Protect(Encoding.UTF8.GetBytes(plain), Entropy, DataProtectionScope.LocalMachine);
            return Convert.ToBase64String(data);
        }

        public string Unprotect(string protectedValue)
        {
            try
            {
                var data = ProtectedData.Unprotect(Convert.FromBase64String(protectedValue), Entropy, DataProtectionScope.LocalMachine);
                return Encoding.UTF8.GetString(data);
            }
            catch (Exception)
            {
                // Mesin berbeda / data rusak: anggap belum ada password agar Admin mengisi ulang di UI.
                return null;
            }
        }
    }
}
