using System.Web.Mvc;
using WebApps.Filters;

namespace WebApps.Controllers
{
    [DomainAuthorize(Roles = "Admin,SuperAdmin")]
    public class TemplatesController : Controller
    {
        public ActionResult Index()
        {
            ViewBag.Title = "Kelola Template";
            return View();
        }
    }
}
