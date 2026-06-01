using HygieneAudit.Application.DTOs;
using HygieneAudit.Application.Services;
using System.Net;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Web.Http;

namespace WebApps.Controllers.Api
{
    [RoutePrefix("api/audits")]
    [Authorize]
    public class AuditsApiController : ApiController
    {
        private readonly IAuditService _auditService;

        public AuditsApiController(IAuditService auditService)
        {
            _auditService = auditService;
        }

        [HttpGet, Route("")]
        public async Task<IHttpActionResult> GetAll()
        {
            // All authenticated roles see all audits; write operations remain PIC-scoped.
            var audits = await _auditService.GetAuditsAsync(0, true);
            return Ok(audits);
        }

        [HttpPost, Route("")]
        public async Task<IHttpActionResult> Create([FromBody] CreateAuditRequest request)
        {
            var audit = await _auditService.CreateAuditAsync(request);
            return Created(new System.Uri($"api/audits/{audit.Id}", System.UriKind.Relative), audit);
        }

        [HttpGet, Route("{id}")]
        public async Task<IHttpActionResult> Get(string id)
        {
            var audit = await _auditService.GetAuditAsync(id);
            if (audit == null) return NotFound();
            return Ok(audit);
        }

        [HttpPut, Route("{id}/items/{templateId}")]
        public async Task<IHttpActionResult> UpdateItem(string id, int templateId, [FromBody] AuditItemUpdate update)
        {
            if (!await CanAccessAsync(id)) return NotFound();
            await _auditService.SaveAuditItemAsync(id, templateId, update);
            return StatusCode(HttpStatusCode.NoContent);
        }

        [HttpPost, Route("{id}/submit")]
        public async Task<IHttpActionResult> Submit(string id)
        {
            if (!await CanAccessAsync(id)) return NotFound();
            await _auditService.SubmitAuditAsync(id);
            return Ok(new { message = "Audit berhasil diselesaikan!" });
        }

        [HttpPost, Route("{id}/draft")]
        public async Task<IHttpActionResult> SaveDraft(string id)
        {
            if (!await CanAccessAsync(id)) return NotFound();
            await _auditService.SaveDraftAsync(id);
            return Ok(new { message = "Draft berhasil disimpan!" });
        }

        private (int userId, bool isAdmin) CurrentUser()
        {
            var identity = System.Web.HttpContext.Current?.User?.Identity as ClaimsIdentity
                           ?? User.Identity as ClaimsIdentity;
            var userId = int.Parse(identity?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
            var role = identity?.FindFirst(ClaimTypes.Role)?.Value ?? "";
            var isAdmin = role == "Admin" || role == "SuperAdmin";
            return (userId, isAdmin);
        }

        private bool CanAccess(AuditResponse audit)
        {
            var (userId, isAdmin) = CurrentUser();
            return isAdmin || audit.PicId == userId;
        }

        private async Task<bool> CanAccessAsync(string id)
        {
            var audit = await _auditService.GetAuditAsync(id);
            return audit != null && CanAccess(audit);
        }
    }
}
