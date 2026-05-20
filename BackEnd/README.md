# Hygiene Audit Backend - .NET 8 Web API

## 📋 Overview
Backend API untuk aplikasi Hygiene Audit PWA (Plaza Indonesia). Mendukung:
- Audit hygiene tenant F&B
- Tenant History Preview
- Excel Reporting
- Offline Sync
- JWT Authentication

## 🏗️ Architecture (Clean Architecture)
```
HygieneAudit/
├── HygieneAudit.Domain/          # Entities, Interfaces, DTOs
├── HygieneAudit.Application/     # Services, Business Logic
├── HygieneAudit.Infrastructure/  # EF Core, Repositories
└── HygieneAudit.API/             # Controllers, Middleware
```

## 🚀 Quick Start

### Prerequisites
- .NET 8 SDK
- SQL Server (LocalDB atau SQL Server Express)

### 1. Clone & Build
```bash
git clone <repo-url>
cd HygieneAudit
dotnet build
```

### 2. Database Setup
```bash
cd src/HygieneAudit.API
dotnet ef database update
```

### 3. Run
```bash
dotnet run
```
API akan berjalan di:
- HTTP: http://localhost:5000
- HTTPS: https://localhost:7000
- Swagger: http://localhost:5000/swagger

## 🔐 Default Accounts
| Username | Password | Role |
|----------|----------|------|
| admin | admin123 | Admin |
| auditor1 | audit123 | Auditor |
| auditor2 | audit123 | Auditor |

## 📡 API Endpoints

### Authentication
- `POST /api/auth/login` - Login

### Audits
- `POST /api/audits` - Create audit
- `GET /api/audits/{id}` - Get audit detail
- `PUT /api/audits/{id}/items/{templateId}` - Update item
- `POST /api/audits/{id}/submit` - Submit audit
- `POST /api/audits/{id}/draft` - Save draft

### Tenants
- `GET /api/tenants` - List tenants
- `GET /api/tenants/{id}/history` - Tenant history preview

### Reports (Admin only)
- `GET /api/reports/latest-per-tenant` - Excel report data
- `GET /api/reports/export-excel` - Export CSV

### Sync
- `POST /api/sync` - Offline sync queue

## 📊 Features

### Tenant History Preview
Saat memilih tenant di audit baru, API mengembalikan:
- Total audit count
- Average pass rate
- Days since last audit
- Trend chart data (6 audits)
- Recent audits list

### Excel Reporting
Admin panel menampilkan:
- Latest audit per tenant
- Pass rate dengan progress bar
- Filter by status/type/search
- Export ke CSV (Excel-compatible)

## 🗄️ Database Schema

### Tables
- **Users** - Admin & Auditor accounts
- **Tenants** - F&B outlets (Gas/Non-Gas)
- **ChecklistTemplates** - 27 audit items
- **Audits** - Audit sessions
- **AuditItems** - Individual checklist items
- **AuditItemPhotos** - Photo evidence
- **SyncQueue** - Offline sync tracking

### Seed Data
- 3 Users (admin, auditor1, auditor2)
- 5 Tenants (Teras by Plataran, Starbucks, Gramedia, H&M, Sushi Tei)
- 27 Checklist Templates (LIFE SAFETY, SIRKULASI UDARA, dll.)

## 🔧 Configuration

### appsettings.json
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=HygieneAudit;..."
  },
  "Jwt": {
    "Key": "your-super-secret-key-min-32-chars-long!!",
    "Issuer": "HygieneAuditAPI",
    "Audience": "HygieneAuditPWA",
    "ExpiryMinutes": 480
  }
}
```

## 📝 Development Notes

### Migration Commands
```bash
# Create migration
dotnet ef migrations add InitialCreate --project src/HygieneAudit.Infrastructure --startup-project src/HygieneAudit.API

# Update database
dotnet ef database update --project src/HygieneAudit.Infrastructure --startup-project src/HygieneAudit.API
```

### CORS
API mengizinkan request dari origin mana saja untuk mendukung PWA.

### JWT Token
Token expires dalam 8 jam (480 menit). Include di header:
```
Authorization: Bearer <token>
```

## 📦 Dependencies
- Microsoft.AspNetCore.Authentication.JwtBearer (8.0.5)
- Microsoft.EntityFrameworkCore.SqlServer (8.0.5)
- Swashbuckle.AspNetCore (6.5.0)
- BCrypt.Net-Next (4.0.3)

## ✅ Testing Checklist
- [ ] Login dengan admin/auditor
- [ ] Create audit baru
- [ ] Update item status (PASS/FAIL)
- [ ] Submit audit (validasi wajib)
- [ ] Tenant history API
- [ ] Excel report API
- [ ] Export CSV
- [ ] Filter reports
- [ ] Offline sync

## 📄 License
Internal Use - Plaza Indonesia
"# HygieneAudit" 
