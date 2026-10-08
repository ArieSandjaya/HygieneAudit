using HygieneAudit.Application.Services;
using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Web.Http;
using WebApps.Helpers;

namespace WebApps.Controllers.Api
{
    [RoutePrefix("api/reports")]
    [Authorize]
    public class ReportsApiController : ApiController
    {
        private readonly IAuditService _auditService;

        public ReportsApiController(IAuditService auditService)
        {
            _auditService = auditService;
        }

        [HttpGet, Route("latest-per-tenant")]
        public async Task<IHttpActionResult> GetLatestPerTenant(
            [FromUri] string status = "all",
            [FromUri] string type = "all",
            [FromUri] string search = "")
        {
            var report = await _auditService.GetExcelReportAsync(status, type, search);
            return Ok(report);
        }

        [HttpGet, Route("export-excel")]
        public async Task<HttpResponseMessage> ExportExcel(
            [FromUri] string status = "all",
            [FromUri] string type = "all",
            [FromUri] string search = "")
        {
            var bytes = await _auditService.ExportExcelAsync(status, type, search, PhotoStorage.UploadsFolder);
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(bytes)
            };
            response.Content.Headers.ContentType =
                new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
            response.Content.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment")
            {
                FileName = $"Report_Audit_Hygiene_{DateTime.Now:yyyy-MM-dd}.xlsx"
            };
            return response;
        }

        // Laporan follow up: satu baris per temuan (item mandatori yang pernah Fail).
        // status: all | unresolved | overdue | resolved
        [HttpGet, Route("followups")]
        public async Task<IHttpActionResult> GetFollowUpReport(
            [FromUri] string status = "all",
            [FromUri] string type = "all",
            [FromUri] string search = "",
            [FromUri] DateTime? dateFrom = null,
            [FromUri] DateTime? dateTo = null)
        {
            var report = await _auditService.GetFollowUpReportAsync(status, type, search, dateFrom, dateTo);
            return Ok(report);
        }

        [HttpGet, Route("followups/export-excel")]
        public async Task<HttpResponseMessage> ExportFollowUpExcel(
            [FromUri] string status = "all",
            [FromUri] string type = "all",
            [FromUri] string search = "",
            [FromUri] DateTime? dateFrom = null,
            [FromUri] DateTime? dateTo = null)
        {
            var bytes = await _auditService.ExportFollowUpReportAsync(status, type, search, dateFrom, dateTo);
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(bytes)
            };
            response.Content.Headers.ContentType =
                new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
            response.Content.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment")
            {
                FileName = $"Report_FollowUp_Hygiene_{DateTime.Now:yyyy-MM-dd}.xlsx"
            };
            return response;
        }

        // Laporan gabungan: hasil audit (nilai awal) + hasil follow up (nilai kini) per audit.
        // status: all | findings | unresolved | overdue | clean
        [HttpGet, Route("audit-followups")]
        public async Task<IHttpActionResult> GetAuditFollowUpReport(
            [FromUri] string status = "all",
            [FromUri] string type = "all",
            [FromUri] string search = "",
            [FromUri] DateTime? dateFrom = null,
            [FromUri] DateTime? dateTo = null)
        {
            return Ok(await _auditService.GetAuditFollowUpReportAsync(status, type, search, dateFrom, dateTo));
        }

        [HttpGet, Route("audit-followups/export-excel")]
        public async Task<HttpResponseMessage> ExportAuditFollowUpExcel(
            [FromUri] string status = "all",
            [FromUri] string type = "all",
            [FromUri] string search = "",
            [FromUri] DateTime? dateFrom = null,
            [FromUri] DateTime? dateTo = null)
        {
            var bytes = await _auditService.ExportAuditFollowUpReportAsync(status, type, search, dateFrom, dateTo);
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(bytes)
            };
            response.Content.Headers.ContentType =
                new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
            response.Content.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment")
            {
                FileName = $"Report_Audit_FollowUp_Hygiene_{DateTime.Now:yyyy-MM-dd}.xlsx"
            };
            return response;
        }
    }
}
