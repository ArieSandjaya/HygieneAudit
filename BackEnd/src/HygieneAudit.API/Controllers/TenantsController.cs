using HygieneAudit.Application.Services;
using HygieneAudit.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HygieneAudit.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TenantsController : ControllerBase
{
    private readonly IAuditService _auditService;
    private readonly IUnitOfWork _unitOfWork;

    public TenantsController(IAuditService auditService, IUnitOfWork unitOfWork)
    {
        _auditService = auditService;
        _unitOfWork = unitOfWork;
    }

    [HttpGet]
    public async Task<ActionResult> GetAll()
    {
        var tenants = await _unitOfWork.Tenants.GetActiveAsync();
        return Ok(tenants);
    }

    [HttpGet("{id}/history")]
    public async Task<ActionResult> GetHistory(int id)
    {
        var history = await _auditService.GetTenantHistoryAsync(id);
        return Ok(history);
    }
}
