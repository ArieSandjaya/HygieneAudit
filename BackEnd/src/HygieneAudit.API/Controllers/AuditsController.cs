using HygieneAudit.Application.DTOs;
using HygieneAudit.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HygieneAudit.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AuditsController : ControllerBase
{
    private readonly IAuditService _auditService;

    public AuditsController(IAuditService auditService)
    {
        _auditService = auditService;
    }

    [HttpPost]
    public async Task<ActionResult> Create([FromBody] CreateAuditRequest request)
    {
        var audit = await _auditService.CreateAuditAsync(request);
        return CreatedAtAction(nameof(Get), new { id = audit.Id }, audit);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult> Get(string id)
    {
        var audit = await _auditService.GetAuditAsync(id);
        if (audit == null) return NotFound();
        return Ok(audit);
    }

    [HttpPut("{id}/items/{templateId}")]
    public async Task<IActionResult> UpdateItem(string id, int templateId, [FromBody] AuditItemUpdate update)
    {
        await _auditService.SaveAuditItemAsync(id, templateId, update);
        return NoContent();
    }

    [HttpPost("{id}/submit")]
    public async Task<IActionResult> Submit(string id)
    {
        await _auditService.SubmitAuditAsync(id);
        return Ok(new { message = "Audit berhasil diselesaikan!" });
    }

    [HttpPost("{id}/draft")]
    public async Task<IActionResult> SaveDraft(string id)
    {
        await _auditService.SaveDraftAsync(id);
        return Ok(new { message = "Draft berhasil disimpan!" });
    }
}
