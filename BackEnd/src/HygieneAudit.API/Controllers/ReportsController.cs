using HygieneAudit.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HygieneAudit.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin,SuperAdmin")]
public class ReportsController : ControllerBase
{
    private readonly IAuditService _auditService;

    public ReportsController(IAuditService auditService)
    {
        _auditService = auditService;
    }

    [HttpGet("latest-per-tenant")]
    public async Task<ActionResult> GetLatestPerTenant(
        [FromQuery] string? status = "all",
        [FromQuery] string? type = "all",
        [FromQuery] string? search = "")
    {
        var report = await _auditService.GetExcelReportAsync(status, type, search);
        return Ok(report);
    }

    [HttpGet("export-excel")]
    public async Task<IActionResult> ExportExcel(
        [FromQuery] string? status = "all",
        [FromQuery] string? type = "all",
        [FromQuery] string? search = "")
    {
        var report = await _auditService.GetExcelReportAsync(status, type, search);
        var excelBytes = await _auditService.ExportExcelAsync(report);

        return File(excelBytes, 
            "text/csv",
            $"Report_Audit_Hygiene_{DateTime.Now:yyyy-MM-dd}.csv");
    }
}
