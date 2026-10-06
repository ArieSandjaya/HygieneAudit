using System.Web.Mvc;
using WebApps.Filters;

namespace WebApps.Controllers
{
    [DomainAuthorize]
    public class FollowUpsController : Controller
    {
        public ActionResult Index()
        {
            ViewBag.Title = "Follow Up";
            return View();
        }

        public ActionResult Detail(string id)
        {
            // Tanpa id, Detail.cshtml akan memakai ViewBag.AuditId null — alihkan ke daftar.
            if (string.IsNullOrWhiteSpace(id))
                return RedirectToAction("Index");

            ViewBag.AuditId = id;
            ViewBag.Title = "Follow Up Audit";
            return View();
        }
    }
}
