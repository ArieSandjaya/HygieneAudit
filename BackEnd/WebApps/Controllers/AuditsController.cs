using System.Web.Mvc;
using WebApps.Filters;

namespace WebApps.Controllers
{
    [DomainAuthorize]
    public class AuditsController : Controller
    {
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
    }
}
