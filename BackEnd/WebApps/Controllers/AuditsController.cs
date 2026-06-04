using HygieneAudit.Application.Services;
using System.Security.Claims;
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
            var identity = User.Identity as ClaimsIdentity;
            var userIdStr = identity?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            ViewBag.CurrentUserId = int.TryParse(userIdStr, out var uid) ? uid : 0;
            ViewBag.IsAdmin = User.IsInRole("Admin") || User.IsInRole("SuperAdmin");
            return View();
        }

        public ActionResult Detail(string id)
        {
            // Guard: tanpa id, view (Detail.cshtml) memanggil ViewBag.AuditId.ToString()
            // pada nilai null → Microsoft.CSharp.RuntimeBinder.RuntimeBinderException
            // ("Cannot perform runtime binding on a null reference"). Alihkan ke daftar.
            if (string.IsNullOrWhiteSpace(id))
                return RedirectToAction("Index");

            ViewBag.AuditId = id;
            ViewBag.Title = "Detail Audit";
            var identity = User.Identity as ClaimsIdentity;
            var userIdStr = identity?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            ViewBag.CurrentUserId = int.TryParse(userIdStr, out var uid) ? uid : 0;
            ViewBag.IsAdmin = User.IsInRole("Admin") || User.IsInRole("SuperAdmin");
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
