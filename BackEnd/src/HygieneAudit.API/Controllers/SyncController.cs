using System.Text.Json;
using HygieneAudit.Application.DTOs;
using HygieneAudit.Application.Services;
using HygieneAudit.Domain.Entities;
using HygieneAudit.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HygieneAudit.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[RequestSizeLimit(52_428_800)]
public class SyncController : ControllerBase
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditService _auditService;
    private static readonly JsonSerializerOptions _jsonOpts =
        new() { PropertyNameCaseInsensitive = true };

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
                var d = JsonSerializer.Deserialize<UpdateItemPayload>(item.Payload, _jsonOpts);
                if (d?.AuditId != null)
                    await _auditService.SaveAuditItemAsync(
                        d.AuditId, d.TemplateId,
                        new AuditItemUpdate { Status = d.Status, Note = d.Note, Photos = d.Photos });
                break;
            }
            case "save_draft":
            {
                var d = JsonSerializer.Deserialize<DraftPayload>(item.Payload, _jsonOpts);
                if (d?.Id != null) await _auditService.SaveDraftAsync(d.Id);
                break;
            }
        }
    }
}

file-scoped internal record UpdateItemPayload(
    string AuditId, int TemplateId,
    string? Status, string? Note, List<string>? Photos);

file-scoped internal record DraftPayload(string Id);
