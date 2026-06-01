using HygieneAudit.Application.Services;
using System.Threading.Tasks;
using System.Web.Mvc;
using WebApps.Filters;

namespace WebApps.Controllers
{
    [DomainAuthorize]
    public class AuditsController : Controller
    {
        private readonly IAuditService _auditService;

        public AuditsController(IAuditService auditService)
        {
            _auditService = auditService;
        }

        public ActionResult Index()
        {
            ViewBag.Title = "Daftar Audit";
            return View();
        }

        public ActionResult Detail(string id)
        {
            ViewBag.AuditId = id;
            ViewBag.Title = "Detail Audit";
            return View();
        }

        public async Task<ActionResult> PrintReport(string id)
        {
            var audit = await _auditService.GetAuditAsync(id);
            if (audit == null) return HttpNotFound();
            return View(audit);
        }
    }
}
