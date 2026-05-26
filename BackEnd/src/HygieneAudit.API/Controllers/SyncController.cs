using HygieneAudit.Application.DTOs;
using HygieneAudit.Application.Services;
using HygieneAudit.Domain.Entities;
using HygieneAudit.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace HygieneAudit.API.Controllers;

[ApiController]
[Route("api/sync")]
[Authorize]
public class SyncController : ControllerBase
{
    private readonly IUnitOfWork  _unitOfWork;
    private readonly IAuditService _auditService;

    private static readonly JsonSerializerSettings _jsonSettings = new JsonSerializerSettings
    {
        ContractResolver = new CamelCasePropertyNamesContractResolver()
    };

    public SyncController(IUnitOfWork unitOfWork, IAuditService auditService)
    {
        _unitOfWork   = unitOfWork;
        _auditService = auditService;
    }

    [HttpPost]
    public async Task<IActionResult> Sync([FromBody] List<SyncQueueItem> items)
    {
        int processed = 0;

        foreach (var item in items)
        {
            try
            {
                await ProcessItem(item);
                item.IsSynced     = true;
                item.ErrorMessage = null;
                processed++;
            }
            catch (Exception ex)
            {
                item.IsSynced     = false;
                item.ErrorMessage = ex.Message;
            }

            await _unitOfWork.SyncQueue.AddAsync(item);
        }

        await _unitOfWork.SaveChangesAsync();
        return Ok(new { processed, total = items.Count });
    }

    private async Task ProcessItem(SyncQueueItem item)
    {
        if (string.IsNullOrEmpty(item.Payload)) return;

        switch (item.Action?.ToLower())
        {
            case "update_item":
            {
                var d = JsonConvert.DeserializeObject<UpdateItemPayload>(item.Payload, _jsonSettings);
                if (d?.AuditId != null)
                    await _auditService.SaveAuditItemAsync(
                        d.AuditId, d.TemplateId,
                        new AuditItemUpdate { Status = d.Status, Note = d.Note, Photos = d.Photos });
                break;
            }
            case "save_draft":
            {
                var d = JsonConvert.DeserializeObject<DraftPayload>(item.Payload, _jsonSettings);
                if (d?.Id != null) await _auditService.SaveDraftAsync(d.Id);
                break;
            }
        }
    }
}

internal class UpdateItemPayload
{
    public string?       AuditId    { get; set; }
    public int           TemplateId { get; set; }
    public string?       Status     { get; set; }
    public string?       Note       { get; set; }
    public List<string>? Photos     { get; set; }
}

internal class DraftPayload
{
    public string? Id { get; set; }
}
