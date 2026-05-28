using System.Web.Mvc;
using WebApps.Filters;

namespace WebApps.Controllers
{
    [DomainAuthorize]
    public class TenantsController : Controller
    {
        public ActionResult Index()
        {
            ViewBag.Title = "Daftar Tenant";
            return View();
        }
    }
}
