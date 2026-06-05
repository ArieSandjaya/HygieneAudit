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
            if (request == null) return BadRequest("Data audit tidak boleh kosong.");
            var (userId, isAdmin) = CurrentUser();
            // Non-admins can only create audits assigned to themselves.
            if (!isAdmin) request.PicId = userId;
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
                    var rawMime = semi > 0 ? meta.Substring(0, semi) : meta;
                    // Only serve known-safe image MIME types to prevent stored-XSS via crafted
                    // data URLs with a text/html or image/svg+xml content type.
                    contentType = (rawMime == "image/jpeg" || rawMime == "image/png" ||
                                   rawMime == "image/gif"  || rawMime == "image/webp")
                        ? rawMime : "image/jpeg";
                    base64 = storedValue.Substring(comma + 1);
                }
                try { bytes = Convert.FromBase64String(base64); }
                catch { return Request.CreateResponse(HttpStatusCode.NotFound); }
            }

            var resp = Request.CreateResponse(HttpStatusCode.OK);
            resp.Content = new ByteArrayContent(bytes);
            resp.Content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
            // Private: the endpoint requires authentication; public caching would allow
            // proxies/CDNs to serve protected photos to unauthenticated clients.
            resp.Headers.CacheControl = new CacheControlHeaderValue
            {
                Private = true,
                MaxAge = TimeSpan.FromDays(30)
            };
            return resp;
        }

        [HttpPost, Route("{id}/items/{templateId}/photos")]
        public async Task<IHttpActionResult> UploadPhoto(string id, int templateId)
        {
            if (!await CanAccessAsync(id)) return NotFound();

            if (!Request.Content.IsMimeMultipartContent())
                return BadRequest("Multipart form data diharapkan.");

            var provider = new MultipartMemoryStreamProvider();
            await Request.Content.ReadAsMultipartAsync(provider);

            HttpContent photoPart = null;
            foreach (var part in provider.Contents)
            {
                var name = part.Headers.ContentDisposition?.Name?.Trim('"');
                if (string.Equals(name, "photo", StringComparison.OrdinalIgnoreCase))
                {
                    photoPart = part;
                    break;
                }
            }

            if (photoPart == null) return BadRequest("Field 'photo' tidak ditemukan.");

            var bytes = await photoPart.ReadAsByteArrayAsync();
            if (bytes.Length == 0) return BadRequest("File kosong.");
            const int MaxPhotoBytes = 10 * 1024 * 1024; // 10 MB
            if (bytes.Length > MaxPhotoBytes)
                return Content(HttpStatusCode.RequestEntityTooLarge, new { message = "Ukuran foto maksimal 10 MB." });

            // Persist to disk outside the web root so the file monitor is never triggered.
            var uploadsPath = PhotoStorage.UploadsFolder;
            Directory.CreateDirectory(uploadsPath);
            var filename = Guid.NewGuid().ToString("N") + ".jpg";
            File.WriteAllBytes(Path.Combine(uploadsPath, filename), bytes);

            int photoId;
            try
            {
                photoId = await _auditService.AddPhotoAsync(id, templateId, filename);
            }
            catch
            {
                // Roll back the orphaned file if the DB insert failed.
                try { File.Delete(Path.Combine(uploadsPath, filename)); } catch { }
                return Content(HttpStatusCode.InternalServerError,
                    new { message = "Gagal menyimpan foto ke database." });
            }

            return Ok(new { url = $"/api/audits/photos/{photoId}" });
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

            var removedPhotos = await _auditService.SaveAuditItemAsync(id, templateId, update);
            // Delete the physical files for any photos that were removed.
            PhotoStorage.DeleteFiles(removedPhotos);
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

        [HttpDelete, Route("{id}")]
        public async Task<IHttpActionResult> Delete(string id)
        {
            var audit = await _auditService.GetAuditAsync(id);
            if (audit == null) return NotFound();

            // Hanya PIC yang melakukan audit yang boleh menghapus — admin sekalipun
            // tidak, kecuali dia memang PIC audit tsb.
            var (userId, _) = CurrentUser();
            if (audit.PicId != userId)
                return Content(HttpStatusCode.Forbidden,
                    new { message = "Hanya PIC yang melakukan audit ini yang dapat menghapusnya." });

            // Hanya DRAFT yang bisa dihapus; service menegakkan ini (→ 400) bila bukan.
            // File foto dihapus setelah baris terhapus.
            var removedPhotos = await _auditService.DeleteDraftAuditAsync(id);
            PhotoStorage.DeleteFiles(removedPhotos);
            return StatusCode(HttpStatusCode.NoContent);
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
