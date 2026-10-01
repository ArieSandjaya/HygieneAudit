using HygieneAudit.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HygieneAudit.Infrastructure.Migrations
{
    [DbContext(typeof(HygieneAuditDbContext))]
    [Migration("20260930150000_AddIsMandatory")]
    public partial class AddIsMandatory : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // defaultValue: true — semua template dan audit yang sudah ada tetap mandatori,
            // sehingga nilai audit lama tidak berubah.
            migrationBuilder.AddColumn<bool>(
                name: "IsMandatory",
                table: "ChecklistTemplates",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsMandatory",
                table: "AuditItems",
                type: "bit",
                nullable: false,
                defaultValue: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "IsMandatory", table: "AuditItems");
            migrationBuilder.DropColumn(name: "IsMandatory", table: "ChecklistTemplates");
        }
    }
}
