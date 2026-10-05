using HygieneAudit.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using System;

#nullable disable

namespace HygieneAudit.Infrastructure.Migrations
{
    [DbContext(typeof(HygieneAuditDbContext))]
    [Migration("20261005190000_AddAuditItemFollowUpDate")]
    public partial class AddAuditItemFollowUpDate : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Nullable: audit lama yang sudah selesai tidak punya tanggal rencana follow up.
            migrationBuilder.AddColumn<DateTime>(
                name: "FollowUpDate",
                table: "AuditItems",
                type: "datetime2",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "FollowUpDate", table: "AuditItems");
        }
    }
}
