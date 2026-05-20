using HygieneAudit.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HygieneAudit.Infrastructure.Data;

public class HygieneAuditDbContext : DbContext
{
    public HygieneAuditDbContext(DbContextOptions<HygieneAuditDbContext> options) : base(options) { }

    public DbSet<User> Users { get; set; }
    public DbSet<Tenant> Tenants { get; set; }
    public DbSet<ChecklistTemplate> ChecklistTemplates { get; set; }
    public DbSet<Audit> Audits { get; set; }
    public DbSet<AuditItem> AuditItems { get; set; }
    public DbSet<AuditItemPhoto> AuditItemPhotos { get; set; }
    public DbSet<SyncQueueItem> SyncQueue { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Audit>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.Tenant).WithMany(t => t.Audits).HasForeignKey(e => e.TenantId);
            entity.HasOne(e => e.Pic).WithMany().HasForeignKey(e => e.PicId);
            entity.HasMany(e => e.Items).WithOne(i => i.Audit).HasForeignKey(i => i.AuditId);
        });

        modelBuilder.Entity<AuditItem>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasMany(e => e.Photos).WithOne(p => p.AuditItem).HasForeignKey(p => p.AuditItemId);
        });

        // Seed Users
        modelBuilder.Entity<User>().HasData(
            new User { Id = 1, Username = "admin", PasswordHash = "$2a$11$vGQn8U.h1TkckZt8Onm1zOQ3YhZ2Z3Z4Z5Z6Z7Z8Z9Z0Z1Z2Z3Z4Z5Z", Name = "Administrator", Role = UserRole.Admin },
            new User { Id = 2, Username = "auditor1", PasswordHash = "$2a$11$vGQn8U.h1TkckZt8Onm1zOQ3YhZ2Z3Z4Z5Z6Z7Z8Z9Z0Z1Z2Z3Z4Z5Z", Name = "Budi Santoso", Role = UserRole.Auditor },
            new User { Id = 3, Username = "auditor2", PasswordHash = "$2a$11$vGQn8U.h1TkckZt8Onm1zOQ3YhZ2Z3Z4Z5Z6Z7Z8Z9Z0Z1Z2Z3Z4Z5Z", Name = "Dewi Kusuma", Role = UserRole.Auditor }
        );

        // Seed Tenants
        modelBuilder.Entity<Tenant>().HasData(
            new Tenant { Id = 1, Name = "Teras by Plataran", UsesGas = true, Category = "Restoran", Floor = "LG" },
            new Tenant { Id = 2, Name = "Starbucks Coffee", UsesGas = true, Category = "Kafe", Floor = "G" },
            new Tenant { Id = 3, Name = "Gramedia Bookstore", UsesGas = false, Category = "Retail", Floor = "2" },
            new Tenant { Id = 4, Name = "H&M Fashion", UsesGas = false, Category = "Retail", Floor = "1" },
            new Tenant { Id = 5, Name = "Sushi Tei", UsesGas = true, Category = "Restoran", Floor = "3" }
        );

        // Seed Checklist Templates (27 items from your HTML)
        var templates = new (string Category, string Name, bool RequiresGas)[]
        {
            ("LIFE SAFETY", "Kondisi Pipa Gas di Area Tenant", true),
            ("LIFE SAFETY", "Interkoneksi Gas dengan Exhaust Fan", true),
            ("LIFE SAFETY", "Sensor Kebocoran Gas", true),
            ("LIFE SAFETY", "Penutup Otomatis (Solenoid Valve)", true),
            ("LIFE SAFETY", "Alarm Warning System", false),
            ("LIFE SAFETY", "Sprinkler, Smoke/Heat Detector", false),
            ("LIFE SAFETY", "Alat Pemadam Api Ringan (APAR)", false),
            ("LIFE SAFETY", "Fire Suppression System", false),
            ("LIFE SAFETY", "Lampu Emergency", false),
            ("LIFE SAFETY", "Signage Exit", false),
            ("LIFE SAFETY", "Kotak P3K/First Aid", false),
            ("SIRKULASI UDARA", "Ducting & Filter Hood", true),
            ("INSTALASI PIPA AIR BERSIH", "Pipa Supply", false),
            ("SALURAN PEMBUANGAN", "Gutter & Floor Drain", false),
            ("SALURAN PEMBUANGAN", "Grease Trap", true),
            ("SALURAN PEMBUANGAN", "Pemilahan Minyak Bekas", true),
            ("INSTALASI LISTRIK", "Instalasi Listrik", false),
            ("INSTALASI LISTRIK", "Panel Listrik", false),
            ("INSTALASI LISTRIK", "Penerangan Area Kitchen", true),
            ("KEBERSIHAN AREA", "Kebersihan", false),
            ("KEBERSIHAN AREA", "Kerapihan", false),
            ("KEBERSIHAN AREA", "Sampah", false),
            ("KEBERSIHAN AREA", "General Cleaning", false),
            ("PEST CONTROL", "Vendor Pest Control", false),
            ("PEST CONTROL", "Tidak Ada Celah (Dinding/Ceiling)", false),
            ("PERSONAL HYGIENE", "Personal Hygiene SOP", false),
            ("SERTIFIKASI", "Sertifikasi Hygiene", false)
        };

        for (int i = 0; i < templates.Length; i++)
        {
            var (cat, name, gas) = templates[i];
            modelBuilder.Entity<ChecklistTemplate>().HasData(
                new ChecklistTemplate { Id = i + 1, Category = cat, Name = name, RequiresGas = gas, DisplayOrder = i + 1 }
            );
        }
    }
}
