using HygieneAudit.Domain.Entities;
using HygieneAudit.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HygieneAudit.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SyncController : ControllerBase
{
    private readonly IUnitOfWork _unitOfWork;

    public SyncController(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    [HttpPost]
    public async Task<IActionResult> Sync([FromBody] List<SyncQueueItem> items)
    {
        foreach (var item in items)
        {
            item.IsSynced = true;
            await _unitOfWork.SyncQueue.AddAsync(item);
        }
        await _unitOfWork.SaveChangesAsync();
        return Ok(new { synced = items.Count });
    }
}
