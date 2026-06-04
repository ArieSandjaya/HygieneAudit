using HygieneAudit.Application.DTOs;
using HygieneAudit.Application.Exceptions;
using HygieneAudit.Application.Services;
using HygieneAudit.Domain.Entities;
using HygieneAudit.Domain.Interfaces;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Web.Http;
using WebApps.Models;

namespace WebApps.Controllers.Api
{
    [RoutePrefix("api/sync")]
    [Authorize]
    public class SyncApiController : ApiController
    {
        private readonly IUnitOfWork _uow;
        private readonly IAuditService _auditService;
        private static readonly JsonSerializerSettings _jsonSettings = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver()
        };

        public SyncApiController(IUnitOfWork uow, IAuditService auditService)
        {
            _uow = uow;
            _auditService = auditService;
        }

        [HttpPost, Route("")]
        public async Task<IHttpActionResult> Sync([FromBody] List<SyncQueueItem> items)
        {
            int processed = 0;

            foreach (var item in items)
            {
                try
                {
                    await ProcessItem(item);
                    item.IsSynced = true;
                    item.ErrorMessage = null;
                    processed++;
                }
                catch (Exception ex)
                {
                    item.IsSynced = false;
                    item.ErrorMessage = ex.Message;
                }
            }
            return Ok(new { processed, total = items.Count });
        }

        private async Task ProcessItem(SyncQueueItem item)
        {
            if (string.IsNullOrEmpty(item.Payload)) return;

            switch (item.Action?.ToLower())
            {
                case "update_item":
                {
                    var d = JsonConvert.DeserializeObject<SyncUpdateItemPayload>(item.Payload, _jsonSettings);
                    if (d?.AuditId != null)
                    {
                        await EnsureAccessAsync(d.AuditId);
                        var removedPhotos = await _auditService.SaveAuditItemAsync(
                            d.AuditId, d.TemplateId,
                            new AuditItemUpdate { Status = d.Status, Note = d.Note, Photos = d.Photos });
                        WebApps.Helpers.PhotoStorage.DeleteFiles(removedPhotos);
                    }
                    break;
                }
                case "save_draft":
                {
                    var d = JsonConvert.DeserializeObject<SyncDraftPayload>(item.Payload, _jsonSettings);
                    if (d?.Id != null)
                    {
                        await EnsureAccessAsync(d.Id);
                        await _auditService.SaveDraftAsync(d.Id);
                    }
                    break;
                }
            }
        }

        // Enforce the same per-audit ownership rule as the REST endpoints:
        // a non-admin may only sync changes for audits assigned to them.
        // Without this, any authenticated user could craft a sync payload
        // targeting another PIC's audit.
        private async Task EnsureAccessAsync(string auditId)
        {
            var audit = await _auditService.GetAuditAsync(auditId);
            if (audit == null) throw new NotFoundException("Audit not found");

            var (userId, isAdmin) = CurrentUser();
            if (!isAdmin && audit.PicId != userId)
                throw new UnauthorizedAccessException("Tidak memiliki akses ke audit ini.");
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
    }
}
