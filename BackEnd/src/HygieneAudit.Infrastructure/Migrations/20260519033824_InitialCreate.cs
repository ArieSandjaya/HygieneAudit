using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace HygieneAudit.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ChecklistTemplates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Category = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RequiresGas = table.Column<bool>(type: "bit", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChecklistTemplates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SyncQueue",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Action = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EntityType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EntityId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Payload = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsSynced = table.Column<bool>(type: "bit", nullable: false),
                    RetryCount = table.Column<int>(type: "int", nullable: false),
                    ErrorMessage = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SyncQueue", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Tenants",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UsesGas = table.Column<bool>(type: "bit", nullable: false),
                    Floor = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Category = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tenants", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Username = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Role = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Audits",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    PicId = table.Column<int>(type: "int", nullable: false),
                    IsGas = table.Column<bool>(type: "bit", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Audits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Audits_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Audits_Users_PicId",
                        column: x => x.PicId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AuditItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AuditId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    TemplateId = table.Column<int>(type: "int", nullable: false),
                    Category = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AuditItems_Audits_AuditId",
                        column: x => x.AuditId,
                        principalTable: "Audits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AuditItemPhotos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AuditItemId = table.Column<int>(type: "int", nullable: false),
                    PhotoUrl = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditItemPhotos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AuditItemPhotos_AuditItems_AuditItemId",
                        column: x => x.AuditItemId,
                        principalTable: "AuditItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "ChecklistTemplates",
                columns: new[] { "Id", "Category", "DisplayOrder", "IsActive", "Name", "RequiresGas" },
                values: new object[,]
                {
                    { 1, "LIFE SAFETY", 1, true, "Kondisi Pipa Gas di Area Tenant", true },
                    { 2, "LIFE SAFETY", 2, true, "Interkoneksi Gas dengan Exhaust Fan", true },
                    { 3, "LIFE SAFETY", 3, true, "Sensor Kebocoran Gas", true },
                    { 4, "LIFE SAFETY", 4, true, "Penutup Otomatis (Solenoid Valve)", true },
                    { 5, "LIFE SAFETY", 5, true, "Alarm Warning System", false },
                    { 6, "LIFE SAFETY", 6, true, "Sprinkler, Smoke/Heat Detector", false },
                    { 7, "LIFE SAFETY", 7, true, "Alat Pemadam Api Ringan (APAR)", false },
                    { 8, "LIFE SAFETY", 8, true, "Fire Suppression System", false },
                    { 9, "LIFE SAFETY", 9, true, "Lampu Emergency", false },
                    { 10, "LIFE SAFETY", 10, true, "Signage Exit", false },
                    { 11, "LIFE SAFETY", 11, true, "Kotak P3K/First Aid", false },
                    { 12, "SIRKULASI UDARA", 12, true, "Ducting & Filter Hood", true },
                    { 13, "INSTALASI PIPA AIR BERSIH", 13, true, "Pipa Supply", false },
                    { 14, "SALURAN PEMBUANGAN", 14, true, "Gutter & Floor Drain", false },
                    { 15, "SALURAN PEMBUANGAN", 15, true, "Grease Trap", true },
                    { 16, "SALURAN PEMBUANGAN", 16, true, "Pemilahan Minyak Bekas", true },
                    { 17, "INSTALASI LISTRIK", 17, true, "Instalasi Listrik", false },
                    { 18, "INSTALASI LISTRIK", 18, true, "Panel Listrik", false },
                    { 19, "INSTALASI LISTRIK", 19, true, "Penerangan Area Kitchen", true },
                    { 20, "KEBERSIHAN AREA", 20, true, "Kebersihan", false },
                    { 21, "KEBERSIHAN AREA", 21, true, "Kerapihan", false },
                    { 22, "KEBERSIHAN AREA", 22, true, "Sampah", false },
                    { 23, "KEBERSIHAN AREA", 23, true, "General Cleaning", false },
                    { 24, "PEST CONTROL", 24, true, "Vendor Pest Control", false },
                    { 25, "PEST CONTROL", 25, true, "Tidak Ada Celah (Dinding/Ceiling)", false },
                    { 26, "PERSONAL HYGIENE", 26, true, "Personal Hygiene SOP", false },
                    { 27, "SERTIFIKASI", 27, true, "Sertifikasi Hygiene", false }
                });

            migrationBuilder.InsertData(
                table: "Tenants",
                columns: new[] { "Id", "Category", "CreatedAt", "Floor", "IsActive", "Name", "UsesGas" },
                values: new object[,]
                {
                    { 1, "Restoran", new DateTime(2026, 5, 19, 3, 38, 23, 618, DateTimeKind.Utc).AddTicks(7724), "LG", true, "Teras by Plataran", true },
                    { 2, "Kafe", new DateTime(2026, 5, 19, 3, 38, 23, 618, DateTimeKind.Utc).AddTicks(7728), "G", true, "Starbucks Coffee", true },
                    { 3, "Retail", new DateTime(2026, 5, 19, 3, 38, 23, 618, DateTimeKind.Utc).AddTicks(7730), "2", true, "Gramedia Bookstore", false },
                    { 4, "Retail", new DateTime(2026, 5, 19, 3, 38, 23, 618, DateTimeKind.Utc).AddTicks(7732), "1", true, "H&M Fashion", false },
                    { 5, "Restoran", new DateTime(2026, 5, 19, 3, 38, 23, 618, DateTimeKind.Utc).AddTicks(7733), "3", true, "Sushi Tei", true }
                });

            migrationBuilder.InsertData(
                table: "Users",
                columns: new[] { "Id", "CreatedAt", "IsActive", "Name", "PasswordHash", "Role", "Username" },
                values: new object[,]
                {
                    { 1, new DateTime(2026, 5, 19, 3, 38, 23, 618, DateTimeKind.Utc).AddTicks(7573), true, "Administrator", "$2a$11$vGQn8U.h1TkckZt8Onm1zOQ3YhZ2Z3Z4Z5Z6Z7Z8Z9Z0Z1Z2Z3Z4Z5Z", 1, "admin" },
                    { 2, new DateTime(2026, 5, 19, 3, 38, 23, 618, DateTimeKind.Utc).AddTicks(7578), true, "Budi Santoso", "$2a$11$vGQn8U.h1TkckZt8Onm1zOQ3YhZ2Z3Z4Z5Z6Z7Z8Z9Z0Z1Z2Z3Z4Z5Z", 0, "auditor1" },
                    { 3, new DateTime(2026, 5, 19, 3, 38, 23, 618, DateTimeKind.Utc).AddTicks(7580), true, "Dewi Kusuma", "$2a$11$vGQn8U.h1TkckZt8Onm1zOQ3YhZ2Z3Z4Z5Z6Z7Z8Z9Z0Z1Z2Z3Z4Z5Z", 0, "auditor2" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditItemPhotos_AuditItemId",
                table: "AuditItemPhotos",
                column: "AuditItemId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditItems_AuditId",
                table: "AuditItems",
                column: "AuditId");

            migrationBuilder.CreateIndex(
                name: "IX_Audits_PicId",
                table: "Audits",
                column: "PicId");

            migrationBuilder.CreateIndex(
                name: "IX_Audits_TenantId",
                table: "Audits",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditItemPhotos");

            migrationBuilder.DropTable(
                name: "ChecklistTemplates");

            migrationBuilder.DropTable(
                name: "SyncQueue");

            migrationBuilder.DropTable(
                name: "AuditItems");

            migrationBuilder.DropTable(
                name: "Audits");

            migrationBuilder.DropTable(
                name: "Tenants");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
