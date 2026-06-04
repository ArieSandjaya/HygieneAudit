using HygieneAudit.Domain.Entities;
using HygieneAudit.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;

namespace WebApps.Helpers
{
    /// <summary>
    /// Seeds baseline data (admin user, sample tenants, checklist templates)
    /// when the app starts and the database is empty. Only runs once; safe to
    /// call on every startup — all operations are guarded by existence checks.
    /// Also creates one completed demo audit with photos so the UI has content
    /// to show right out of the box.
    /// </summary>
    public static class DevDataSeeder
    {
        public static void Seed(string connectionString, string uploadsFolder)
        {
            var options = new DbContextOptionsBuilder<HygieneAuditDbContext>()
                .UseSqlServer(connectionString)
                .Options;

                // Replace C# 8.0 using declaration with explicit using statement for C# 7.3 compatibility
            using (var db = new HygieneAuditDbContext(options))
            {
                SeedUsers(db);
                SeedTenants(db);
                SeedTemplates(db);
                SeedDemoAudit(db, uploadsFolder);
            }
        }

        // ── Users ────────────────────────────────────────────────────────────

        static void SeedUsers(HygieneAuditDbContext db)
        {
            if (db.Set<User>().Any()) return;

            db.Set<User>().AddRange(new[]
            {
                new User
                {
                    Username = "admin",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin123!"),
                    Name = "Administrator",
                    Role = UserRole.SuperAdmin,
                },
                new User
                {
                    Username = "auditor1",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("Auditor123!"),
                    Name = "Budi Santoso",
                    Role = UserRole.Auditor,
                },
                new User
                {
                    Username = "auditor2",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("Auditor123!"),
                    Name = "Siti Rahayu",
                    Role = UserRole.Auditor,
                },
            });
            db.SaveChanges();
        }

        // ── Tenants ──────────────────────────────────────────────────────────

        static void SeedTenants(HygieneAuditDbContext db)
        {
            if (db.Set<Tenant>().Any()) return;

            db.Set<Tenant>().AddRange(new[]
            {
                new Tenant { Name = "Restoran Padang Jaya",  Floor = "Lantai 1", Category = "F&B", UsesGas = true  },
                new Tenant { Name = "Kedai Soto Bu Yem",     Floor = "Lantai 1", Category = "F&B", UsesGas = true  },
                new Tenant { Name = "Bakery & Coffee Corner", Floor = "Lantai 2", Category = "F&B", UsesGas = false },
                new Tenant { Name = "Warung Nasi Campur",     Floor = "Lantai 2", Category = "F&B", UsesGas = true  },
                new Tenant { Name = "Foodcourt Timur",        Floor = "Lantai B1", Category = "F&B", UsesGas = true },
            });
            db.SaveChanges();
        }

        // ── Checklist Templates ───────────────────────────────────────────────

        static void SeedTemplates(HygieneAuditDbContext db)
        {
            if (db.Set<ChecklistTemplate>().Any()) return;

            var templates = new List<ChecklistTemplate>();
            int order = 1;

            void Add(string cat, string name, bool gas = false) =>
                templates.Add(new ChecklistTemplate
                {
                    Category = cat, Name = name,
                    RequiresGas = gas, DisplayOrder = order++, IsActive = true
                });

            // Kebersihan Area
            Add("Kebersihan Area", "Lantai bersih, tidak ada kotoran/sampah");
            Add("Kebersihan Area", "Permukaan meja/counter bebas dari debu dan noda");
            Add("Kebersihan Area", "Dinding dan langit-langit bersih");
            Add("Kebersihan Area", "Tempat sampah tertutup dan tidak penuh");
            Add("Kebersihan Area", "Area penyimpanan bahan makanan rapi dan bersih");

            // Keamanan Pangan
            Add("Keamanan Pangan", "Makanan matang disimpan terpisah dari bahan mentah");
            Add("Keamanan Pangan", "Suhu lemari pendingin ≤ 4°C");
            Add("Keamanan Pangan", "Bahan makanan bertanggal kadaluarsa jelas");
            Add("Keamanan Pangan", "Makanan ditutup/terlindung dari kontaminasi");
            Add("Keamanan Pangan", "FIFO (First In First Out) diterapkan");
            Add("Keamanan Pangan", "Tidak ada bahan makanan kadaluarsa");

            // Peralatan & Peralatan Masak
            Add("Peralatan Masak", "Peralatan masak bersih dan tidak berkarat");
            Add("Peralatan Masak", "Talenan bersih dan tidak retak");
            Add("Peralatan Masak", "Pisau bersih dan disimpan dengan aman");
            Add("Peralatan Masak", "Wadah/tempat makan bersih dan tidak retak");
            Add("Peralatan Masak", "Wastafel berfungsi baik dengan sabun tersedia");

            // Higiene Personal
            Add("Higiene Personal", "Petugas menggunakan seragam bersih dan rapi");
            Add("Higiene Personal", "Petugas mencuci tangan sebelum menyentuh makanan");
            Add("Higiene Personal", "Rambut tertutup/terikat saat memasak");
            Add("Higiene Personal", "Tidak ada perhiasan/jam tangan saat memasak");
            Add("Higiene Personal", "Luka terbuka ditutup dengan plester berwarna");

            // Instalasi Gas (hanya jika pakai gas)
            Add("Instalasi Gas", "Regulator gas terpasang dengan benar",      gas: true);
            Add("Instalasi Gas", "Selang gas tidak bocor (uji busa sabun)",   gas: true);
            Add("Instalasi Gas", "Tabung gas di tempat berventilasi",          gas: true);
            Add("Instalasi Gas", "APAR (alat pemadam) tersedia & tidak kedaluwarsa", gas: true);
            Add("Instalasi Gas", "Katup gas ditutup saat tidak digunakan",    gas: true);

            // Dokumentasi & Prosedur
            Add("Dokumentasi", "SOP kebersihan terpasang dan diikuti");
            Add("Dokumentasi", "Log pembersihan harian terisi lengkap");
            Add("Dokumentasi", "Sertifikat laik sehat masih berlaku");

            db.Set<ChecklistTemplate>().AddRange(templates);
            db.SaveChanges();
        }

        // ── Demo Audit ────────────────────────────────────────────────────────

        static void SeedDemoAudit(HygieneAuditDbContext db, string uploadsFolder)
        {
            // Hanya buat 1 demo audit
            if (db.Set<Audit>().Any()) return;

            var admin = db.Set<User>().First();
            var tenant = db.Set<Tenant>().First();
            var templates = db.Set<ChecklistTemplate>()
                .Where(t => t.IsActive && (!t.RequiresGas || tenant.UsesGas))
                .OrderBy(t => t.Category)
                .ThenBy(t => t.DisplayOrder)
                .ToList();

            var audit = new Audit
            {
                Date = DateTime.UtcNow.Date,
                TenantId = tenant.Id,
                PicId = admin.Id,
                IsGas = tenant.UsesGas,
                Status = AuditStatus.Completed,
                CompletedAt = DateTime.UtcNow,
                Items = new List<AuditItem>(),
            };

            int idx = 0;
            foreach (var t in templates)
            {
                var status = (idx % 5 == 4) ? AuditItemStatus.Fail : AuditItemStatus.Pass;
                var item = new AuditItem
                {
                    TemplateId = t.Id,
                    Category = t.Category,
                    Name = t.Name,
                    Status = status,
                    Note = status == AuditItemStatus.Fail
                        ? "Ditemukan pelanggaran — perlu tindakan korektif segera."
                        : null,
                    Photos = new List<AuditItemPhoto>(),
                };

                // Buat foto demo
                var filename = SaveDemoPhoto(uploadsFolder, t.Name, status, idx);
                if (filename != null)
                    item.Photos.Add(new AuditItemPhoto { PhotoUrl = filename });

                audit.Items.Add(item);
                idx++;
            }

            db.Set<Audit>().Add(audit);
            db.SaveChanges();
        }

        // ── Photo generation (GDI+ / System.Drawing) ─────────────────────────

        static readonly (int R, int G, int B)[] PassColors =
        {
            (22, 197, 94), (16, 185, 129), (52, 211, 153), (5, 150, 105),
        };
        static readonly (int R, int G, int B)[] FailColors =
        {
            (239, 68, 68), (220, 38, 38), (248, 113, 113), (185, 28, 28),
        };

        static string SaveDemoPhoto(string uploadsFolder, string itemName, AuditItemStatus status, int index)
        {
            if (string.IsNullOrEmpty(uploadsFolder)) return null;
            try
            {
                Directory.CreateDirectory(uploadsFolder);
                var colors = status == AuditItemStatus.Pass ? PassColors : FailColors;
                var (r, g, b) = colors[index % colors.Length];

                using (var bmp = new Bitmap(320, 240))
                using (var g2 = Graphics.FromImage(bmp))
                using (var white = new SolidBrush(Color.White))
                using (var fntBig = new Font("Arial", 22f, FontStyle.Bold))
                using (var fntSmall = new Font("Arial", 12f, FontStyle.Regular))
                using (var fntTiny = new Font("Arial", 9f, FontStyle.Regular))
                {
                    g2.Clear(Color.FromArgb(r, g, b));

                    var statusLabel = status == AuditItemStatus.Pass ? "✓  PASS" : "✗  FAIL";
                    var sf = new StringFormat { Alignment = StringAlignment.Center };

                    // Status label
                    g2.DrawString(statusLabel, fntBig, white, new RectangleF(0, 20, 320, 50), sf);

                    // Item name (wrapped)
                    g2.DrawString(itemName, fntSmall, white, new RectangleF(16, 85, 288, 80), sf);

                    // Timestamp
                    var ts = $"Demo — {DateTime.Now:dd/MM/yyyy HH:mm}";
                    g2.DrawString(ts, fntTiny, white, new RectangleF(0, 210, 320, 20), sf);

                    var filename = Guid.NewGuid().ToString("N") + ".jpg";
                    bmp.Save(Path.Combine(uploadsFolder, filename), ImageFormat.Jpeg);
                    return filename;
                }
            }
            catch
            {
                return null;
            }
        }
    }
}
