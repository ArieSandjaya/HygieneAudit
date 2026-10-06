using System.Web.Mvc;
using WebApps.Filters;

namespace WebApps.Controllers
{
    // Laporan dapat diakses semua role yang sudah login (sama seperti daftar Audit dan Follow Up).
    [DomainAuthorize]
    public class ReportsController : Controller
    {
        public ActionResult Index()
        {
            ViewBag.Title = "Laporan";
            return View();
        }
    }
}
