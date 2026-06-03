using HygieneAudit.Application.DTOs;
using HygieneAudit.Application.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Web.Http;
using WebApps.Helpers;

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

        [HttpGet, Route("photos/{photoId:int}")]
        public async Task<HttpResponseMessage> GetPhoto(int photoId)
        {
            var storedValue = await _auditService.GetPhotoUrlAsync(photoId);
            if (string.IsNullOrEmpty(storedValue))
                return Request.CreateResponse(HttpStatusCode.NotFound);

            byte[] bytes;
            var contentType = "image/jpeg";

            if (PhotoStorage.IsFileName(storedValue))
            {
                // New path: value is a filename — read from disk (current folder,
                // then legacy App_Data location for photos saved before the move).
                var filePath = PhotoStorage.ResolveExistingPath(storedValue);
                if (filePath == null)
                    return Request.CreateResponse(HttpStatusCode.NotFound);
                bytes = File.ReadAllBytes(filePath);
                if (storedValue.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                    contentType = "image/png";
            }
            else
            {
                // Legacy path: full base64 data URL still in DB
                var base64 = storedValue;
                var comma = storedValue.IndexOf(',');
                if (storedValue.StartsWith("data:") && comma > 0)
                {
                    var meta = storedValue.Substring(5, comma - 5);
                    var semi = meta.IndexOf(';');
                    contentType = semi > 0 ? meta.Substring(0, semi) : meta;
                    base64 = storedValue.Substring(comma + 1);
                }
                try { bytes = Convert.FromBase64String(base64); }
                catch { return Request.CreateResponse(HttpStatusCode.NotFound); }
            }

            var resp = Request.CreateResponse(HttpStatusCode.OK);
            resp.Content = new ByteArrayContent(bytes);
            resp.Content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
            resp.Headers.CacheControl = new CacheControlHeaderValue
            {
                Public = true,
                MaxAge = TimeSpan.FromDays(365)
            };
            return resp;
        }

        [HttpPut, Route("{id}/items/{templateId}")]
        public async Task<IHttpActionResult> UpdateItem(string id, int templateId, [FromBody] AuditItemUpdate update)
        {
            if (!await CanAccessAsync(id)) return NotFound();

            // Save any incoming base64 photos to disk before the service layer sees them.
            if (update?.Photos != null && update.Photos.Count > 0)
            {
                var uploadsPath = PhotoStorage.UploadsFolder;
                var processed = new List<string>();
                foreach (var entry in update.Photos)
                {
                    if (string.IsNullOrWhiteSpace(entry)) continue;
                    if (entry.StartsWith("data:"))
                    {
                        try
                        {
                            processed.Add(PhotoStorage.SaveFromDataUrl(entry, uploadsPath));
                        }
                        catch (Exception ex)
                        {
                            // Surface a clean 500 instead of an unhandled crash.
                            return Content(HttpStatusCode.InternalServerError,
                                new { message = "Gagal menyimpan foto: " + ex.Message });
                        }
                    }
                    else
                    {
                        processed.Add(entry); // existing reference URL — pass through
                    }
                }
                update.Photos = processed;
            }

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
