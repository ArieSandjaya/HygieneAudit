using HygieneAudit.Application.DTOs;
using HygieneAudit.Domain.Entities;
using HygieneAudit.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HygieneAudit.API.Controllers;

[ApiController]
[Route("api/templates")]
[Authorize]
public class TemplatesController : ControllerBase
{
    private readonly IUnitOfWork _uow;

    public TemplatesController(IUnitOfWork uow) => _uow = uow;

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var templates = await _uow.Templates.GetAllAsync();
        var result = templates
            .Where(t => t.IsActive)
            .OrderBy(t => t.Category)
            .ThenBy(t => t.DisplayOrder)
            .Select(t => new TemplateResponse
            {
                Id           = t.Id,
                Category     = t.Category,
                Name         = t.Name,
                RequiresGas  = t.RequiresGas,
                DisplayOrder = t.DisplayOrder,
                IsActive     = t.IsActive,
            });
        return Ok(result);
    }

    [HttpPost]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<IActionResult> Create([FromBody] CreateTemplateRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Category) || string.IsNullOrWhiteSpace(req.Name))
            return BadRequest(new { message = "Kategori dan nama wajib diisi." });

        var template = new ChecklistTemplate
        {
            Category     = req.Category,
            Name         = req.Name,
            RequiresGas  = req.RequiresGas,
            DisplayOrder = req.DisplayOrder,
        };

        await _uow.Templates.AddAsync(template);
        await _uow.SaveChangesAsync();

        return Created(
            $"api/templates/{template.Id}",
            new TemplateResponse
            {
                Id = template.Id, Category = template.Category, Name = template.Name,
                RequiresGas = template.RequiresGas, DisplayOrder = template.DisplayOrder,
                IsActive = template.IsActive
            });
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateTemplateRequest req)
    {
        var template = await _uow.Templates.GetByIdAsync(id);
        if (template == null) return NotFound();

        if (req.Category     != null) template.Category     = req.Category;
        if (req.Name         != null) template.Name         = req.Name;
        if (req.RequiresGas  != null) template.RequiresGas  = req.RequiresGas.Value;
        if (req.DisplayOrder != null) template.DisplayOrder = req.DisplayOrder.Value;
        if (req.IsActive     != null) template.IsActive     = req.IsActive.Value;

        await _uow.Templates.UpdateAsync(template);
        await _uow.SaveChangesAsync();

        return Ok(new TemplateResponse
        {
            Id = template.Id, Category = template.Category, Name = template.Name,
            RequiresGas = template.RequiresGas, DisplayOrder = template.DisplayOrder,
            IsActive = template.IsActive
        });
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<IActionResult> Delete(int id)
    {
        var template = await _uow.Templates.GetByIdAsync(id);
        if (template == null) return NotFound();

        template.IsActive = false;
        await _uow.Templates.UpdateAsync(template);
        await _uow.SaveChangesAsync();

        return NoContent();
    }
}
