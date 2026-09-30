using System;
using HygieneAudit.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HygieneAudit.Infrastructure.Migrations
{
    [DbContext(typeof(HygieneAuditDbContext))]
    [Migration("20260930090000_AddAuditFollowUps")]
    public partial class AddAuditFollowUps : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuditFollowUps",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AuditItemId = table.Column<int>(type: "int", nullable: false),
                    Date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PicId = table.Column<int>(type: "int", nullable: false),
                    Result = table.Column<int>(type: "int", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditFollowUps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AuditFollowUps_AuditItems_AuditItemId",
                        column: x => x.AuditItemId,
                        principalTable: "AuditItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AuditFollowUps_Users_PicId",
                        column: x => x.PicId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AuditFollowUpPhotos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AuditFollowUpId = table.Column<int>(type: "int", nullable: false),
                    PhotoUrl = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditFollowUpPhotos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AuditFollowUpPhotos_AuditFollowUps_AuditFollowUpId",
                        column: x => x.AuditFollowUpId,
                        principalTable: "AuditFollowUps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditFollowUps_AuditItemId",
                table: "AuditFollowUps",
                column: "AuditItemId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditFollowUps_PicId",
                table: "AuditFollowUps",
                column: "PicId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditFollowUpPhotos_AuditFollowUpId",
                table: "AuditFollowUpPhotos",
                column: "AuditFollowUpId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "AuditFollowUpPhotos");
            migrationBuilder.DropTable(name: "AuditFollowUps");
        }
    }
}
