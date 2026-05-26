using HygieneAudit.Application.DTOs;
using HygieneAudit.Application.Services;
using HygieneAudit.Domain.Entities;
using HygieneAudit.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HygieneAudit.API.Controllers;

[ApiController]
[Route("api/tenants")]
[Authorize]
public class TenantsController : ControllerBase
{
    private readonly IAuditService _auditService;
    private readonly IUnitOfWork   _unitOfWork;

    public TenantsController(IAuditService auditService, IUnitOfWork unitOfWork)
    {
        _auditService = auditService;
        _unitOfWork   = unitOfWork;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var tenants = await _unitOfWork.Tenants.GetActiveAsync();
        return Ok(tenants);
    }

    [HttpGet("{id}/history")]
    public async Task<IActionResult> GetHistory(int id)
    {
        var history = await _auditService.GetTenantHistoryAsync(id);
        return Ok(history);
    }

    [HttpPost]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<IActionResult> Create([FromBody] CreateTenantRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Name))
            return BadRequest(new { message = "Nama tenant wajib diisi." });

        var tenant = new Tenant
        {
            Name     = req.Name,
            UsesGas  = req.UsesGas,
            Floor    = req.Floor,
            Category = req.Category,
        };

        await _unitOfWork.Tenants.AddAsync(tenant);
        await _unitOfWork.SaveChangesAsync();

        return Created($"api/tenants/{tenant.Id}", tenant);
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateTenantRequest req)
    {
        var tenant = await _unitOfWork.Tenants.GetByIdAsync(id);
        if (tenant == null) return NotFound();

        if (req.Name     != null) tenant.Name     = req.Name;
        if (req.UsesGas  != null) tenant.UsesGas  = req.UsesGas.Value;
        if (req.Floor    != null) tenant.Floor    = req.Floor;
        if (req.Category != null) tenant.Category = req.Category;
        if (req.IsActive != null) tenant.IsActive = req.IsActive.Value;

        await _unitOfWork.Tenants.UpdateAsync(tenant);
        await _unitOfWork.SaveChangesAsync();

        return Ok(tenant);
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<IActionResult> Delete(int id)
    {
        var tenant = await _unitOfWork.Tenants.GetByIdAsync(id);
        if (tenant == null) return NotFound();

        tenant.IsActive = false;
        await _unitOfWork.Tenants.UpdateAsync(tenant);
        await _unitOfWork.SaveChangesAsync();

        return NoContent();
    }
}
