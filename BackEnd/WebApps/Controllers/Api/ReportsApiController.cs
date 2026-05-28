using HygieneAudit.Application.Services;
using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Web.Http;

namespace WebApps.Controllers.Api
{
    [RoutePrefix("api/reports")]
    [Authorize(Roles = "Admin,SuperAdmin")]
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
            var report = await _auditService.GetExcelReportAsync(status, type, search);
            var csvBytes = await _auditService.ExportExcelAsync(report);

            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(csvBytes)
            };
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
            response.Content.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment")
            {
                FileName = $"Report_Audit_Hygiene_{DateTime.Now:yyyy-MM-dd}.csv"
            };
            return response;
        }
    }
}
