using HygieneAudit.Application.DTOs;
using HygieneAudit.Application.Services;
using HygieneAudit.Domain.Entities;
using HygieneAudit.Domain.Interfaces;
using System.Net;
using System.Threading.Tasks;
using System.Web.Http;

namespace WebApps.Controllers.Api
{
    [RoutePrefix("api/tenants")]
    [Authorize]
    public class TenantsApiController : ApiController
    {
        private readonly IAuditService _auditService;
        private readonly IUnitOfWork _uow;

        public TenantsApiController(IAuditService auditService, IUnitOfWork uow)
        {
            _auditService = auditService;
            _uow = uow;
        }

        [HttpGet, Route("")]
        public async Task<IHttpActionResult> GetAll()
        {
            var tenants = await _uow.Tenants.GetActiveAsync();
            return Ok(tenants);
        }

        [HttpGet, Route("{id}/history")]
        public async Task<IHttpActionResult> GetHistory(int id)
        {
            var history = await _auditService.GetTenantHistoryAsync(id);
            return Ok(history);
        }

        [HttpPost, Route("")]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<IHttpActionResult> Create([FromBody] CreateTenantRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.Name))
                return Content(HttpStatusCode.BadRequest, new { message = "Nama tenant wajib diisi." });

            var tenant = new Tenant
            {
                Name = req.Name,
                UsesGas = req.UsesGas,
                Floor = req.Floor,
                Category = req.Category,
            };

            await _uow.Tenants.AddAsync(tenant);
            await _uow.SaveChangesAsync();

            return Created(new System.Uri($"api/tenants/{tenant.Id}", System.UriKind.Relative), tenant);
        }

        [HttpPut, Route("{id:int}")]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<IHttpActionResult> Update(int id, [FromBody] UpdateTenantRequest req)
        {
            var tenant = await _uow.Tenants.GetByIdAsync(id);
            if (tenant == null) return NotFound();

            if (req.Name != null) tenant.Name = req.Name;
            if (req.UsesGas != null) tenant.UsesGas = req.UsesGas.Value;
            if (req.Floor != null) tenant.Floor = req.Floor;
            if (req.Category != null) tenant.Category = req.Category;
            if (req.IsActive != null) tenant.IsActive = req.IsActive.Value;

            await _uow.Tenants.UpdateAsync(tenant);
            await _uow.SaveChangesAsync();

            return Ok(tenant);
        }

        [HttpDelete, Route("{id:int}")]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<IHttpActionResult> Delete(int id)
        {
            var tenant = await _uow.Tenants.GetByIdAsync(id);
            if (tenant == null) return NotFound();

            tenant.IsActive = false;
            await _uow.Tenants.UpdateAsync(tenant);
            await _uow.SaveChangesAsync();

            return StatusCode(HttpStatusCode.NoContent);
        }
    }
}
