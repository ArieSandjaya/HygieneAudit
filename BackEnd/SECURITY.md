# 🔒 Security Guide - Hygiene Audit Backend

## Overview
Dokumen ini menjelaskan aspek keamanan dari Hygiene Audit Backend .NET 8.

## ✅ Keamanan yang Sudah Diimplementasikan

### 1. Authentication & Authorization
- **JWT Token** dengan expiry time (configurable)
- **Role-based access control** (Auditor, Admin, SuperAdmin)
- **BCrypt password hashing** (salt + hash, work factor adaptive)
- **Token validation** (issuer, audience, signature, expiration)

### 2. Input Validation
- **Model validation** via [ApiController]
- **Anti-XSS** validation attributes
- **SQL Injection prevention** via EF Core parameterized queries
- **HTML sanitization** untuk user input

### 3. HTTPS & Transport Security
- **HTTPS redirection** enforced
- **HSTS** (HTTP Strict Transport Security) header
- **TLS 1.2+** required

### 4. Security Headers
```
X-Content-Type-Options: nosniff
X-Frame-Options: DENY
X-XSS-Protection: 1; mode=block
Referrer-Policy: strict-origin-when-cross-origin
Content-Security-Policy: default-src 'self'
Permissions-Policy: camera=(), microphone=(), geolocation=()
Strict-Transport-Security: max-age=31536000
```

### 5. CORS Policy
- **Development**: AllowAnyOrigin (untuk PWA testing)
- **Production**: Restricted to specific domains

### 6. Rate Limiting
- **100 requests/minute** per client
- **Queue limit**: 2 requests

### 7. Audit Logging
- Semua request dicatat (user, path, method, timestamp)
- Error logging tanpa expose sensitive data

## ⚠️ Yang HARUS Diubah Sebelum Production

### 1. JWT Secret Key
**File**: `appsettings.Production.json`
```json
"Jwt": {
  "Key": "GENERATE_32+_CHAR_RANDOM_STRING_HERE!!!"
}
```
**Cara generate**:
```bash
openssl rand -base64 32
```

### 2. Database Connection String
**File**: `appsettings.Production.json`
- Gunakan **SQL Authentication** (bukan Windows Auth)
- Password **strong** (min 16 chars, mixed case, symbols)
- **Encrypt=True** dan **TrustServerCertificate=False**
- Gunakan **connection string dari Azure Key Vault** (recommended)

### 3. CORS Allowed Origins
**File**: `appsettings.Production.json`
```json
"Cors": {
  "AllowedOrigins": [
    "https://yourdomain.com",
    "https://app.yourdomain.com"
  ]
}
```

### 4. Seed Data Passwords
**File**: `HygieneAuditDbContext.cs`
- Password seed data saat ini adalah **placeholder**
- Ganti dengan **BCrypt hash** dari password yang kuat
- Atau **hapus seed users** dan buat via admin panel

## 🔴 Risiko Keamanan yang Perlu Diperhatikan

### Risiko: HIGH
| Risiko | Mitigasi |
|--------|----------|
| JWT Key lemah | Gunakan 32+ char random string |
| Hardcoded passwords | Pindahkan ke environment variables |
| SQL Injection | ✅ Sudah aman (EF Core parameterized) |
| XSS | ✅ Sudah aman (anti-XSS headers + validation) |

### Risiko: MEDIUM
| Risiko | Mitigasi |
|--------|----------|
| CORS AllowAnyOrigin (dev) | Restrict di production |
| Error detail exposure | Hanya di development mode |
| Rate limiting | Sudah ada (100 req/min) |

### Risiko: LOW
| Risiko | Mitigasi |
|--------|----------|
| Photo upload | Validasi file type & size |
| Audit data exposure | Role-based access control |
| Brute force login | Implementasi lockout policy |

## 🛡️ Rekomendasi Tambahan

### 1. Azure Key Vault (Recommended)
```csharp
// Program.cs
builder.Configuration.AddAzureKeyVault(
    new Uri($"https://{builder.Configuration["KeyVault:Name"]}.vault.azure.net/"),
    new DefaultAzureCredential());
```

### 2. Account Lockout
```csharp
// Tambahkan di AuthService
if (failedAttempts >= 5)
{
    user.LockoutEnd = DateTime.UtcNow.AddMinutes(30);
    await _unitOfWork.SaveChangesAsync();
}
```

### 3. Password Policy
```csharp
// Validasi password saat create user
public static bool IsStrongPassword(string password)
{
    return password.Length >= 12 
        && password.Any(char.IsUpper)
        && password.Any(char.IsLower)
        && password.Any(char.IsDigit)
        && password.Any(c => !char.IsLetterOrDigit(c));
}
```

### 4. File Upload Security
```csharp
// Untuk photo upload
var allowedTypes = new[] { "image/jpeg", "image/png", "image/webp" };
var maxSize = 5 * 1024 * 1024; // 5MB

if (!allowedTypes.Contains(file.ContentType))
    return BadRequest("Invalid file type");

if (file.Length > maxSize)
    return BadRequest("File too large");
```

### 5. API Versioning
```bash
dotnet add package Microsoft.AspNetCore.Mvc.Versioning
```

### 6. Health Checks
```csharp
builder.Services.AddHealthChecks()
    .AddDbContextCheck<HygieneAuditDbContext>();
```

## 📋 Pre-Production Checklist

- [ ] Ganti JWT Key (32+ char random)
- [ ] Ganti database connection string (production server)
- [ ] Restrict CORS origins
- [ ] Ganti/hapus seed data passwords
- [ ] Enable HTTPS only
- [ ] Configure rate limiting
- [ ] Setup logging (Application Insights/Serilog)
- [ ] Enable WAF (Web Application Firewall)
- [ ] Setup monitoring & alerting
- [ ] Penetration testing

## 🔗 Referensi
- [OWASP Top 10](https://owasp.org/www-project-top-ten/)
- [Microsoft Security Best Practices](https://docs.microsoft.com/en-us/aspnet/core/security/)
- [JWT Security Best Practices](https://tools.ietf.org/html/rfc8725)
