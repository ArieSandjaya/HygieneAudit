using System.Web.Mvc;
using WebApps.Filters;

namespace WebApps.Controllers
{
    [DomainAuthorize(Roles = "Admin,SuperAdmin")]
    public class UsersController : Controller
    {
        public ActionResult Index()
        {
            ViewBag.Title = "Kelola Pengguna";
            return View();
        }
    }
}
