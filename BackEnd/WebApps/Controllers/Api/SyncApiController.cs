using HygieneAudit.Application.DTOs;
using HygieneAudit.Application.Services;
using HygieneAudit.Domain.Entities;
using HygieneAudit.Domain.Interfaces;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
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

                await _uow.SyncQueue.AddAsync(item);
            }

            await _uow.SaveChangesAsync();
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
                        await _auditService.SaveAuditItemAsync(
                            d.AuditId, d.TemplateId,
                            new AuditItemUpdate { Status = d.Status, Note = d.Note, Photos = d.Photos });
                    break;
                }
                case "save_draft":
                {
                    var d = JsonConvert.DeserializeObject<SyncDraftPayload>(item.Payload, _jsonSettings);
                    if (d?.Id != null) await _auditService.SaveDraftAsync(d.Id);
                    break;
                }
            }
        }
    }
}
