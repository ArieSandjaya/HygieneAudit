using System.Web.Mvc;
using WebApps.Filters;

namespace WebApps.Controllers
{
    [DomainAuthorize(Roles = "Admin,SuperAdmin")]
    public class ReportsController : Controller
    {
        public ActionResult Index()
        {
            ViewBag.Title = "Laporan";
            return View();
        }
    }
}
