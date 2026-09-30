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
    // Follow up atas item audit yang FAIL. Semua role yang login boleh melihat;
    // mencatat follow up hanya untuk admin atau PIC pelaksana audit tsb.
    [RoutePrefix("api/followups")]
    [Authorize]
    public class FollowUpsApiController : ApiController
    {
        private const int MaxPhotosPerFollowUp = 4;

        private readonly IAuditService _auditService;

        public FollowUpsApiController(IAuditService auditService)
        {
            _auditService = auditService;
        }

        // Daftar audit COMPLETED yang belum 100% (atau pernah di-follow up).
        [HttpGet, Route("")]
        public async Task<IHttpActionResult> GetAll()
        {
            return Ok(await _auditService.GetFollowUpAuditsAsync());
        }

        [HttpGet, Route("{auditId}")]
        public async Task<IHttpActionResult> Get(string auditId)
        {
            var detail = await _auditService.GetFollowUpDetailAsync(auditId);
            if (detail == null) return NotFound();
            return Ok(detail);
        }

        [HttpPost, Route("{auditId}/items/{auditItemId:int}")]
        public async Task<IHttpActionResult> Add(string auditId, int auditItemId, [FromBody] AddFollowUpRequest request)
        {
            if (request == null) return BadRequest("Data follow up tidak boleh kosong.");

            var audit = await _auditService.GetAuditAsync(auditId);
            if (audit == null) return NotFound();

            var (userId, isAdmin) = CurrentUser();
            if (!isAdmin && audit.PicId != userId)
                return Content(HttpStatusCode.Forbidden,
                    new { message = "Hanya admin atau PIC yang melakukan audit ini yang dapat melakukan follow up." });

            // Simpan foto (data URL) ke disk lebih dulu; service hanya menyimpan nama file.
            var saved = new List<string>();
            if (request.Photos != null && request.Photos.Count > 0)
            {
                if (request.Photos.Count > MaxPhotosPerFollowUp)
                    return BadRequest("Maksimal " + MaxPhotosPerFollowUp + " foto per follow up.");

                var uploadsPath = PhotoStorage.UploadsFolder;
                foreach (var entry in request.Photos)
                {
                    if (string.IsNullOrWhiteSpace(entry) || !entry.StartsWith("data:")) continue;
                    try
                    {
                        saved.Add(PhotoStorage.SaveFromDataUrl(entry, uploadsPath));
                    }
                    catch (Exception ex)
                    {
                        PhotoStorage.DeleteFiles(saved);
                        return Content(HttpStatusCode.InternalServerError,
                            new { message = "Gagal menyimpan foto: " + ex.Message });
                    }
                }
            }
            request.Photos = saved;

            try
            {
                var result = await _auditService.AddFollowUpAsync(auditId, auditItemId, userId, request);
                return Ok(result);
            }
            catch
            {
                // Validasi/DB gagal: jangan tinggalkan file foto yatim.
                PhotoStorage.DeleteFiles(saved);
                throw;
            }
        }

        [HttpGet, Route("photos/{photoId:int}")]
        public async Task<HttpResponseMessage> GetPhoto(int photoId)
        {
            var stored = await _auditService.GetFollowUpPhotoUrlAsync(photoId);
            if (string.IsNullOrEmpty(stored) || !PhotoStorage.IsFileName(stored))
                return Request.CreateResponse(HttpStatusCode.NotFound);

            var filePath = PhotoStorage.ResolveExistingPath(stored);
            if (filePath == null) return Request.CreateResponse(HttpStatusCode.NotFound);

            var contentType = stored.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ? "image/png" : "image/jpeg";
            var resp = Request.CreateResponse(HttpStatusCode.OK);
            resp.Content = new ByteArrayContent(File.ReadAllBytes(filePath));
            resp.Content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
            resp.Headers.CacheControl = new CacheControlHeaderValue { Private = true, MaxAge = TimeSpan.FromDays(30) };
            return resp;
        }

        private (int userId, bool isAdmin) CurrentUser()
        {
            var identity = System.Web.HttpContext.Current?.User?.Identity as ClaimsIdentity
                           ?? User.Identity as ClaimsIdentity;
            var userId = int.Parse(identity?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
            var role = identity?.FindFirst(ClaimTypes.Role)?.Value ?? "";
            return (userId, role == "Admin" || role == "SuperAdmin");
        }
    }
}
