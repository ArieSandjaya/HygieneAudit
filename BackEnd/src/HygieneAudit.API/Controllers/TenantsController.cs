using System.Threading.Tasks;
using System.Web.Http;
using HygieneAudit.Application.Services;
using HygieneAudit.Domain.Interfaces;

namespace HygieneAudit.API.Controllers
{
    [RoutePrefix("api/tenants")]
    [Authorize]
    public class TenantsController : ApiController
    {
        private readonly IAuditService _auditService;
        private readonly IUnitOfWork _unitOfWork;

        public TenantsController(IAuditService auditService, IUnitOfWork unitOfWork)
        {
            _auditService = auditService;
            _unitOfWork = unitOfWork;
        }

        [HttpGet]
        [Route("")]
        public async Task<IHttpActionResult> GetAll()
        {
            var tenants = await _unitOfWork.Tenants.GetActiveAsync();
            return Ok(tenants);
        }

        [HttpGet]
        [Route("{id}/history")]
        public async Task<IHttpActionResult> GetHistory(int id)
        {
            var history = await _auditService.GetTenantHistoryAsync(id);
            return Ok(history);
        }
    }
}
