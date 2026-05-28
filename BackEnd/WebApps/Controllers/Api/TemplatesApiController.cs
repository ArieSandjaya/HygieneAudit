using HygieneAudit.Application.DTOs;
using HygieneAudit.Domain.Entities;
using HygieneAudit.Domain.Interfaces;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web.Http;

namespace WebApps.Controllers.Api
{
    [RoutePrefix("api/templates")]
    [Authorize]
    public class TemplatesApiController : ApiController
    {
        private readonly IUnitOfWork _uow;

        public TemplatesApiController(IUnitOfWork uow) => _uow = uow;

        [HttpGet, Route("")]
        public async Task<IHttpActionResult> GetAll()
        {
            var templates = await _uow.Templates.GetAllAsync();
            var result = templates
                .Where(t => t.IsActive)
                .OrderBy(t => t.Category)
                .ThenBy(t => t.DisplayOrder)
                .Select(t => new TemplateResponse
                {
                    Id = t.Id,
                    Category = t.Category,
                    Name = t.Name,
                    RequiresGas = t.RequiresGas,
                    DisplayOrder = t.DisplayOrder,
                    IsActive = t.IsActive,
                });
            return Ok(result);
        }

        [HttpPost, Route("")]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<IHttpActionResult> Create([FromBody] CreateTemplateRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.Category) || string.IsNullOrWhiteSpace(req.Name))
                return Content(HttpStatusCode.BadRequest, new { message = "Kategori dan nama wajib diisi." });

            var template = new ChecklistTemplate
            {
                Category = req.Category,
                Name = req.Name,
                RequiresGas = req.RequiresGas,
                DisplayOrder = req.DisplayOrder,
            };

            await _uow.Templates.AddAsync(template);
            await _uow.SaveChangesAsync();

            return Created(
                new System.Uri($"api/templates/{template.Id}", System.UriKind.Relative),
                new TemplateResponse { Id = template.Id, Category = template.Category, Name = template.Name, RequiresGas = template.RequiresGas, DisplayOrder = template.DisplayOrder, IsActive = template.IsActive });
        }

        [HttpPut, Route("{id:int}")]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<IHttpActionResult> Update(int id, [FromBody] UpdateTemplateRequest req)
        {
            var template = await _uow.Templates.GetByIdAsync(id);
            if (template == null) return NotFound();

            if (req.Category != null) template.Category = req.Category;
            if (req.Name != null) template.Name = req.Name;
            if (req.RequiresGas != null) template.RequiresGas = req.RequiresGas.Value;
            if (req.DisplayOrder != null) template.DisplayOrder = req.DisplayOrder.Value;
            if (req.IsActive != null) template.IsActive = req.IsActive.Value;

            await _uow.Templates.UpdateAsync(template);
            await _uow.SaveChangesAsync();

            return Ok(new TemplateResponse { Id = template.Id, Category = template.Category, Name = template.Name, RequiresGas = template.RequiresGas, DisplayOrder = template.DisplayOrder, IsActive = template.IsActive });
        }

        [HttpDelete, Route("{id:int}")]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<IHttpActionResult> Delete(int id)
        {
            var template = await _uow.Templates.GetByIdAsync(id);
            if (template == null) return NotFound();

            template.IsActive = false;
            await _uow.Templates.UpdateAsync(template);
            await _uow.SaveChangesAsync();

            return StatusCode(HttpStatusCode.NoContent);
        }
    }
}
