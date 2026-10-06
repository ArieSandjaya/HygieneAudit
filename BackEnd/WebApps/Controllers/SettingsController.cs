using System.Web.Mvc;
using WebApps.Filters;

namespace WebApps.Controllers
{
    [DomainAuthorize(Roles = "Admin,SuperAdmin")]
    public class SettingsController : Controller
    {
        public ActionResult Notifications()
        {
            ViewBag.Title = "Pengaturan Notifikasi";
            return View();
        }
    }
}
